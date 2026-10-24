using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Invitations;

public partial class InvitationServiceManageTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 17, 30, 0, DateTimeKind.Utc);

    private static readonly RoleGrantRole OwnerRole =
        new(Guid.NewGuid(), "Owner", SystemRoleKind.Owner, Permissions.All.ToHashSet());

    private static readonly RoleGrantRole MemberRole =
        new(Guid.NewGuid(), "Member", SystemRoleKind.Member, new HashSet<string>());

    private static readonly RoleGrantRole PeopleRole =
        new(Guid.NewGuid(), "Nhân sự", null, new HashSet<string> { Permissions.OrganizationMembersManage });

    private static readonly RoleGrantRole TesterRole =
        new(Guid.NewGuid(), "Tester", null, new HashSet<string> { Permissions.FilesManage });

    private static readonly RoleGrantRole ItLeadRole = new(
        Guid.NewGuid(),
        "Trưởng IT",
        null,
        new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin });

    private static readonly RoleGrantRole[] OrganizationRoles =
        [OwnerRole, MemberRole, PeopleRole, TesterRole, ItLeadRole];

    public static TheoryData<string> Operations => new() { "resend", "revoke" };

    // An in-memory organization behind substitutes; the transaction stub restores the invitations when it throws.
    private sealed record Fixture
    {
        private readonly List<OrganizationMember> members = [];
        private readonly List<Account> accounts = [];
        private readonly List<OrganizationInvitation> invitations = [];

        public Fixture()
        {
            Organization = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));

            UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => RunInTransactionAsync(
                    callInfo.Arg<Func<CancellationToken, Task>>()!, callInfo.Arg<CancellationToken>()));
            UnitOfWork.Repository<Organization, Guid>().Returns(Organizations);
            UnitOfWork.Repository<OrganizationMember, Guid>().Returns(Members);
            UnitOfWork.Repository<Account, Guid>().Returns(Accounts);
            UnitOfWork.Repository<OrganizationInvitation, Guid>().Returns(Invitations);

            Organizations.GetByIdAsync(Organization.Id, Arg.Any<CancellationToken>()).Returns(_ => Organization);
            RepositoryPredicateStub.StubFirstOrDefault(Members, members);
            RepositoryPredicateStub.StubFirstOrDefault(Accounts, accounts);
            RepositoryPredicateStub.StubFirstOrDefault(Invitations, invitations);
            Invitations.When(repository => repository.Delete(Arg.Any<OrganizationInvitation>()))
                .Do(callInfo => invitations.RemoveAll(
                    invitation => invitation.Id == callInfo.Arg<OrganizationInvitation>()!.Id));
            Invitations.When(repository => repository.AddAsync(
                    Arg.Any<OrganizationInvitation>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => invitations.Add(callInfo.Arg<OrganizationInvitation>()!));

            Reader.LoadCallerAsync(Organization.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(_ => CallerGrant);
            Reader.LoadRolesAsync(Organization.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)OrganizationRoles
                        .Where(role => ids.Contains(role.Id) && !GoneRoleIds.Contains(role.Id))
                        .ToList();
                });

            AuditTrail.When(trail => trail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Recorded.Add(callInfo.Arg<AuditEvent>()!));
            EmailSender.When(sender => sender.SendAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Mailed.Add((callInfo.ArgAt<string>(0), callInfo.ArgAt<string>(2))));

            // Anything read before the lock could be stale by the time the lock is held (TECH00 §5.4).
            OrganizationLock.When(organizationLock => organizationLock.AcquireAsync(
                    Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => ReadBeforeLock = ReadBeforeLock
                    || Reader.ReceivedCalls().Any()
                    || Members.ReceivedCalls().Any()
                    || Accounts.ReceivedCalls().Any()
                    || Invitations.ReceivedCalls().Any()
                    || Organizations.ReceivedCalls().Any());

            Clock.UtcNow.Returns(_ => Now);
            Tenant.OrganizationId.Returns(_ => Organization.Id);
            CurrentUser.UserId.Returns(_ => CallerId.ToString());

            Service = new InvitationService(
                CurrentUser,
                Tenant,
                UnitOfWork,
                OrganizationLock,
                Reader,
                Queries,
                AuditTrail,
                EmailSender,
                Clock,
                Options.Create(new EmailSettings { FrontendBaseUrl = "https://hub.example.com/" }),
                Substitute.For<ITenantAccessService>(),
                Substitute.For<IPermissionResolver>(),
                Substitute.For<ILogger<InvitationService>>());
        }

        public Organization Organization { get; }

        public InvitationService Service { get; }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IRepository<Organization, Guid> Organizations { get; } = Substitute.For<IRepository<Organization, Guid>>();

        public IRepository<OrganizationMember, Guid> Members { get; } =
            Substitute.For<IRepository<OrganizationMember, Guid>>();

        public IRepository<Account, Guid> Accounts { get; } = Substitute.For<IRepository<Account, Guid>>();

        public IRepository<OrganizationInvitation, Guid> Invitations { get; } =
            Substitute.For<IRepository<OrganizationInvitation, Guid>>();

        public IRoleGrantReader Reader { get; } = Substitute.For<IRoleGrantReader>();

        public IInvitationQueries Queries { get; } = Substitute.For<IInvitationQueries>();

        public IOrganizationLock OrganizationLock { get; } = Substitute.For<IOrganizationLock>();

        public IAuditTrail AuditTrail { get; } = Substitute.For<IAuditTrail>();

        public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

        public IDateTimeProvider Clock { get; } = Substitute.For<IDateTimeProvider>();

        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

        public ICurrentTenantProvider Tenant { get; } = Substitute.For<ICurrentTenantProvider>();

        public List<AuditEvent> Recorded { get; } = [];

        public List<(string To, string Body)> Mailed { get; } = [];

        public HashSet<Guid> GoneRoleIds { get; } = [];

        public IReadOnlyList<OrganizationInvitation> Stored => invitations;

        public bool ReadBeforeLock { get; private set; }

        public Exception? ThrownFromTransaction { get; private set; }

        public Guid CallerId { get; private set; } = Guid.NewGuid();

        public RoleGrantCaller CallerGrant { get; set; } = new(Guid.Empty, false, new HashSet<string>());

        public void SetCaller(params RoleGrantRole[] roles)
        {
            Account account = AddAccount($"caller-{Guid.NewGuid():N}@example.com");
            members.Add(OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, account.Id)));
            CallerId = account.Id;
            CallerGrant = new RoleGrantCaller(
                account.Id,
                roles.Any(role => role.SystemKind == SystemRoleKind.Owner),
                roles.SelectMany(role => role.Permissions).ToHashSet());
        }

        public Account AddAccount(string email)
        {
            string handle = $"u{Guid.NewGuid():N}"[..12];
            Account account = Account.Create(new AccountParams("Người dùng", handle, email));
            accounts.Add(account);
            return account;
        }

        public void AddMember(string email)
        {
            Account account = AddAccount(email);
            members.Add(OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, account.Id)));
        }

        public OrganizationInvitation AddInvitation(
            string email, RoleGrantRole role, DateTime issuedAt, Guid? organizationId = null)
        {
            string hash = TokenHash.Compute($"old-{Guid.NewGuid():N}");
            OrganizationInvitation invitation = OrganizationInvitation.Create(
                new OrganizationInvitationParams(organizationId ?? Organization.Id, email, role.Id, hash, issuedAt));
            invitations.Add(invitation);
            return invitation;
        }

        public void DeactivateCaller() => members.Single(member => member.AccountId == CallerId).Deactivate();

        public void FailMail() =>
            EmailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new ServiceUnavailableException(ApplicationMessages.EmailDeliveryFailed)));

        public Task<InvitationDto> ResendAsync(Guid invitationId) =>
            Service.ResendAsync(invitationId, CancellationToken.None);

        public Task RevokeAsync(Guid invitationId) => Service.RevokeOrDeleteAsync(invitationId, CancellationToken.None);

        public Task RunAsync(string operation, Guid invitationId) =>
            operation == "resend" ? ResendAsync(invitationId) : RevokeAsync(invitationId);

        public void StubPage(PageRequest request, int totalCount, params OrganizationInvitation[] rows) =>
            Queries.ListAsync(Organization.Id, request, Arg.Any<CancellationToken>())
                .Returns(new PagedResult<InvitationRow>(
                    rows.Select(row => new InvitationRow(row.Id, row.Email, row.RoleId, row.ExpiresAt)).ToList(),
                    totalCount,
                    request.PageNumber,
                    request.PageSize));

        public async Task AssertNothingWrittenAsync()
        {
            await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            await Invitations.DidNotReceive().AddAsync(Arg.Any<OrganizationInvitation>(), Arg.Any<CancellationToken>());
            Invitations.DidNotReceive().Delete(Arg.Any<OrganizationInvitation>());
            await EmailSender.DidNotReceive().SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        private async Task RunInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            List<OrganizationInvitation> before = [.. invitations];
            try
            {
                await operation(token);
            }
            catch (Exception exception)
            {
                ThrownFromTransaction = exception;
                invitations.Clear();
                invitations.AddRange(before);
                throw;
            }
        }
    }

    private static Fixture CreateFixture() => new();

    private static string TokenIn(string body) => InviteLink().Match(body).Groups[1].Value;

    [GeneratedRegex("#/invite\\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    // Prelude shared by ResendAsync and RevokeOrDeleteAsync: lock, gate re-check, members.manage from the database

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_Any_AcquiresLockBeforeAnyRead(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation invitation = f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-1));

        await f.RunAsync(operation, invitation.Id);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Assert.False(f.ReadBeforeLock);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_CallerNoLongerActiveMemberAfterGate_Throws403AccessDenied(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.DeactivateCaller();
        OrganizationInvitation invitation = f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.RunAsync(operation, invitation.Id));

        await f.AssertNothingWrittenAsync();
        Assert.Same(invitation, Assert.Single(f.Stored));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_OrganizationSuspendedAfterGate_Throws403Suspended(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, Now));
        OrganizationInvitation invitation = f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationSuspended, () => f.RunAsync(operation, invitation.Id));

        await f.AssertNothingWrittenAsync();
    }

    // members.manage lost after the gate (stale cache): BR-58 alone would let a Member invitation through.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_CallerLostMembersManageAfterGate_Throws403AccessDenied(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(TesterRole);
        OrganizationInvitation invitation = f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.RunAsync(operation, invitation.Id));

        await f.AssertNothingWrittenAsync();
    }

    // AC-310, BR-45, F17: another organization's invitation reads as missing, and the 404 names only the id.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_OtherOrganizationsInvitation_Throws404AndLeavesItAlone(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation foreign = f.AddInvitation("y@x.com", MemberRole, Now.AddDays(-1), Guid.NewGuid());

        await ApplicationAssert.AssertNotFoundAsync<OrganizationInvitation>(
            foreign.Id, () => f.RunAsync(operation, foreign.Id));

        await f.AssertNothingWrittenAsync();
        Assert.Same(foreign, Assert.Single(f.Stored));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_UnknownInvitation_Throws404WithoutEmail(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-1));
        Guid unknownId = Guid.NewGuid();

        NotFoundException exception =
            await Assert.ThrowsAsync<NotFoundException>(() => f.RunAsync(operation, unknownId));

        Assert.Equal([nameof(OrganizationInvitation), unknownId], exception.Args);
        Assert.DoesNotContain("b@x.com", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        await f.AssertNothingWrittenAsync();
    }

    // AC-253, AC-254, AC-255, AC-269, AC-270, AC-370: the invitation's role must be within the caller's reach.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_CannotTakeAwayRole_Throws403NoAuditNoDelete(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation invitation = f.AddInvitation("b@congty.vn", ItLeadRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.RunAsync(operation, invitation.Id));

        await f.AssertNothingWrittenAsync();
        Assert.Same(invitation, Assert.Single(f.Stored));
    }

    // BR-58 point 1: only an Owner touches an Owner invitation, even a caller holding every other permission.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task ResendOrRevoke_OwnerRoleInvitationByNonOwner_Throws403OwnerOnly(string operation)
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole, ItLeadRole, TesterRole);
        f.CallerGrant = f.CallerGrant with { Permissions = Permissions.All.ToHashSet() };
        OrganizationInvitation invitation = f.AddInvitation("boss@x.com", OwnerRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => f.RunAsync(operation, invitation.Id));

        await f.AssertNothingWrittenAsync();
        Assert.Same(invitation, Assert.Single(f.Stored));
    }

    // ResendAsync

    // AC-267, AC-373, D4: same address and role, a new id, a fresh 7×24 h from now, and one InvitationSent entry.
    [Fact]
    public async Task ResendAsync_Expired_ReplacesWithSameRoleNewIdFreshExpiry()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation old = f.AddInvitation("c@congty.vn", MemberRole, Now.AddDays(-9));

        InvitationDto dto = await f.ResendAsync(old.Id);

        OrganizationInvitation fresh = Assert.Single(f.Stored);
        Assert.NotEqual(old.Id, fresh.Id);
        Assert.Equal("c@congty.vn", fresh.Email);
        Assert.Equal(MemberRole.Id, fresh.RoleId);
        Assert.Equal(Now + TimeSpan.FromHours(168), fresh.ExpiresAt);
        f.Invitations.Received(1).Delete(old);

        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.InvitationSent, recorded.Type);
        Assert.Equal(fresh.Id, recorded.SubjectId);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal("c@congty.vn", recorded.Parameters["email"]);
        Assert.Equal(new AuditRoleRef(MemberRole.Id, "Member", SystemRoleKind.Member), recorded.Parameters["role"]);

        Assert.Equal(fresh.Id, dto.Id);
        Assert.Equal(InvitationStatus.Pending, dto.Status);
        Assert.Equal(fresh.ExpiresAt, dto.ExpiresAt);
        Assert.Equal(new RoleRefDto(MemberRole.Id, "Member", SystemRoleKind.Member), dto.Role);
        Assert.True(dto.CanResend);
        Assert.True(dto.CanRevokeOrDelete);
        Assert.Equal("c@congty.vn", Assert.Single(f.Mailed).To);
    }

    // BR-63 point 3: the pending code stops working at once; the mailed code is the new invitation's.
    [Fact]
    public async Task ResendAsync_Pending_ReplacesAndOldCodeNoLongerFound()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation old = f.AddInvitation("a@congty.vn", TesterRole, Now.AddDays(-1));

        await f.ResendAsync(old.Id);

        Assert.DoesNotContain(f.Stored, invitation => invitation.TokenHash == old.TokenHash);
        OrganizationInvitation fresh = Assert.Single(f.Stored);
        Assert.Equal(TesterRole.Id, fresh.RoleId);
        Assert.Equal(TokenHash.Compute(TokenIn(Assert.Single(f.Mailed).Body)), fresh.TokenHash);
    }

    // AC-254: an invitation beyond the caller is not replaced and no mail leaves.
    [Fact]
    public async Task ResendAsync_CannotGrantRole_Throws403AndOldKept()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation old = f.AddInvitation("b@congty.vn", ItLeadRole, Now.AddDays(-9));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.ResendAsync(old.Id));

        await f.AssertNothingWrittenAsync();
        Assert.Same(old, Assert.Single(f.Stored));
    }

    // BR-63 point 4, C9: the address joined since the invitation was sent.
    [Fact]
    public async Task ResendAsync_EmailBecameActiveMember_Throws409InvitationEmailAlreadyMember()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation old = f.AddInvitation("m@congty.vn", MemberRole, Now.AddDays(-9));
        f.AddMember("m@congty.vn");

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(() => f.ResendAsync(old.Id));

        Assert.Equal(ApplicationMessages.InvitationEmailAlreadyMember, exception.Message);
        Assert.DoesNotContain("m@congty.vn", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        await f.AssertNothingWrittenAsync();
        Assert.Same(old, Assert.Single(f.Stored));
    }

    // D2: BR-58 (step 6) answers before the 409 (step 7), as for API-11.
    [Fact]
    public async Task ResendAsync_EmailBecameActiveMemberAndCannotGrant_Throws403Not409()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation old = f.AddInvitation("m@congty.vn", ItLeadRole, Now.AddDays(-9));
        f.AddMember("m@congty.vn");

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.ResendAsync(old.Id));

        await f.AssertNothingWrittenAsync();
    }

    // §6.2: the 503 leaves the transaction delegate, so the replacement rolls back and the old invitation stays.
    [Fact]
    public async Task ResendAsync_SmtpFails_Throws503OldKept()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation old = f.AddInvitation("b@x.com", MemberRole, Now.AddDays(-9));
        f.FailMail();

        ServiceUnavailableException exception =
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => f.ResendAsync(old.Id));

        Assert.Equal(ApplicationMessages.InvitationEmailDeliveryFailed, exception.Message);
        Assert.Same(exception, f.ThrownFromTransaction);
        Assert.Same(old, Assert.Single(f.Stored));
    }

    // RevokeOrDeleteAsync

    // AC-272: a pending invitation is revoked; the entry is built from it before the row goes.
    [Fact]
    public async Task RevokeOrDeleteAsync_Pending_RecordsInvitationRevokedAndDeletes()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation invitation = f.AddInvitation("a@congty.vn", MemberRole, Now.AddDays(-1));

        await f.RevokeAsync(invitation.Id);

        Assert.Empty(f.Stored);
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.InvitationRevoked, recorded.Type);
        Assert.Equal(ActorKind.Account, recorded.ActorKind);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(invitation.Id, recorded.SubjectId);
        Assert.Equal(["email", "role"], recorded.Parameters.Keys.Order());
        Assert.Equal("a@congty.vn", recorded.Parameters["email"]);
        Assert.Equal(new AuditRoleRef(MemberRole.Id, "Member", SystemRoleKind.Member), recorded.Parameters["role"]);
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.Invitations.Delete(invitation);
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // AC-268, D5: expired from ExpiresAt on (exactly 7×24 h after sending counts as expired).
    [Theory]
    [InlineData(-168)]
    [InlineData(-200)]
    public async Task RevokeOrDeleteAsync_Expired_RecordsInvitationDeletedAndDeletes(int issuedHoursAgo)
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation invitation = f.AddInvitation("c@congty.vn", MemberRole, Now.AddHours(issuedHoursAgo));

        await f.RevokeAsync(invitation.Id);

        Assert.Empty(f.Stored);
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.InvitationDeleted, recorded.Type);
        Assert.Equal(invitation.Id, recorded.SubjectId);
        Assert.Equal("c@congty.vn", recorded.Parameters["email"]);
        Assert.Equal(new AuditRoleRef(MemberRole.Id, "Member", SystemRoleKind.Member), recorded.Parameters["role"]);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeOrDeleteAsync_OwnerRevokesOwnerInvitation_Deletes()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation invitation = f.AddInvitation("boss@x.com", OwnerRole, Now.AddDays(-1));

        await f.RevokeAsync(invitation.Id);

        Assert.Empty(f.Stored);
        Assert.Equal(AuditEventType.InvitationRevoked, Assert.Single(f.Recorded).Type);
    }

    // ListAsync

    // TECH00 §5.3: flags from the same policy as the writes, inputs read from the database, no lock.
    [Fact]
    public async Task ListAsync_FlagsAndLockReasonFromPolicy()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation member = f.AddInvitation("a@x.com", MemberRole, Now.AddDays(-1));
        OrganizationInvitation itLead = f.AddInvitation("b@x.com", ItLeadRole, Now.AddDays(-2));
        OrganizationInvitation owner = f.AddInvitation("c@x.com", OwnerRole, Now.AddDays(-3));
        PageRequest request = new();
        f.StubPage(request, 3, member, itLead, owner);

        PagedResult<InvitationDto> page = await f.Service.ListAsync(request, CancellationToken.None);

        Assert.Equal([member.Id, itLead.Id, owner.Id], page.Items.Select(dto => dto.Id));
        Assert.Equal([true, false, false], page.Items.Select(dto => dto.CanResend));
        Assert.Equal([true, false, false], page.Items.Select(dto => dto.CanRevokeOrDelete));
        Assert.Equal(
            [null, RoleLockReason.InsufficientRolePermissions, RoleLockReason.OwnerOnly],
            page.Items.Select(dto => dto.LockReason));
        Assert.Equal(new RoleRefDto(ItLeadRole.Id, "Trưởng IT", null), page.Items[1].Role);
        Assert.Equal("b@x.com", page.Items[1].Email);
        Assert.Equal(itLead.ExpiresAt, page.Items[1].ExpiresAt);
        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // BR-75: expired invitations stay listed, labelled from ExpiresAt against the clock (ExpiresAt == now is expired).
    [Fact]
    public async Task ListAsync_StatusFromClock_PendingAndExpired()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation pending = f.AddInvitation("a@x.com", MemberRole, Now.AddHours(-167));
        OrganizationInvitation boundary = f.AddInvitation("b@x.com", MemberRole, Now.AddHours(-168));
        OrganizationInvitation expired = f.AddInvitation("c@x.com", MemberRole, Now.AddDays(-30));
        PageRequest request = new();
        f.StubPage(request, 3, pending, boundary, expired);

        PagedResult<InvitationDto> page = await f.Service.ListAsync(request, CancellationToken.None);

        Assert.Equal(
            [InvitationStatus.Pending, InvitationStatus.Expired, InvitationStatus.Expired],
            page.Items.Select(dto => dto.Status));
    }

    [Fact]
    public async Task ListAsync_Page_KeepsTotalsAndLoadsRolesOnceForDistinctIds()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation one = f.AddInvitation("a@x.com", MemberRole, Now);
        OrganizationInvitation two = f.AddInvitation("b@x.com", MemberRole, Now);
        OrganizationInvitation three = f.AddInvitation("c@x.com", TesterRole, Now);
        PageRequest request = new(2, 3);
        f.StubPage(request, 7, one, two, three);

        PagedResult<InvitationDto> page = await f.Service.ListAsync(request, CancellationToken.None);

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(3, page.PageSize);
        await f.Reader.Received(1).LoadRolesAsync(
            f.Organization.Id,
            Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids != null && ids.Count == 2 && ids.Contains(MemberRole.Id) && ids.Contains(TesterRole.Id)),
            Arg.Any<CancellationToken>());
        await f.Reader.Received(1).LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
    }

    // Read without the lock: a role deleted in between takes its invitations with it (cascade), so the row is gone.
    [Fact]
    public async Task ListAsync_RoleGoneBetweenReads_LeavesInvitationOut()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation kept = f.AddInvitation("a@x.com", MemberRole, Now);
        OrganizationInvitation orphan = f.AddInvitation("b@x.com", TesterRole, Now);
        f.GoneRoleIds.Add(TesterRole.Id);
        PageRequest request = new();
        f.StubPage(request, 2, kept, orphan);

        PagedResult<InvitationDto> page = await f.Service.ListAsync(request, CancellationToken.None);

        Assert.Equal([kept.Id], page.Items.Select(dto => dto.Id));
    }

    // BR-75, T9: the database decides who may see invitation emails; a stale cache that still grants access does not.
    [Fact]
    public async Task ListAsync_CallerWithoutMembersManageInDatabase_Throws403AndReadsNoInvitations()
    {
        Fixture f = CreateFixture();
        f.SetCaller(TesterRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.ListAsync(new PageRequest(), CancellationToken.None));

        await f.Queries.DidNotReceive().ListAsync(
            Arg.Any<Guid>(), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_PersonalContext_Throws403AccessDenied()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.Tenant.OrganizationId.Returns((Guid?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.ListAsync(new PageRequest(), CancellationToken.None));
    }
}
