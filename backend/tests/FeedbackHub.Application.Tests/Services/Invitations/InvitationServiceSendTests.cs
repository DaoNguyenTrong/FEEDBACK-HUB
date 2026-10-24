using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

public partial class InvitationServiceSendTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 17, 30, 0, DateTimeKind.Utc);

    private static readonly RoleGrantRole OwnerRole =
        new(Guid.NewGuid(), "Owner", SystemRoleKind.Owner, Permissions.All.ToHashSet());

    private static readonly RoleGrantRole AdminRole = new(
        Guid.NewGuid(),
        "Admin",
        SystemRoleKind.Admin,
        new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin });

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
        new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.ApiKeysManage });

    private static readonly RoleGrantRole[] OrganizationRoles =
        [OwnerRole, AdminRole, MemberRole, PeopleRole, TesterRole, ItLeadRole];

    // An in-memory organization behind substitutes, so tests assert both calls and resulting state.
    private sealed record Fixture
    {
        private readonly List<OrganizationMember> members = [];
        private readonly List<Account> accounts = [];
        private readonly List<OrganizationInvitation> invitations = [];
        private bool inTransaction;

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
                .Do(callInfo => invitations.Remove(callInfo.Arg<OrganizationInvitation>()!));
            Invitations.When(repository => repository.AddAsync(Arg.Any<OrganizationInvitation>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => invitations.Add(callInfo.Arg<OrganizationInvitation>()!));

            Reader.LoadCallerAsync(Organization.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(_ => CallerGrant);
            Reader.LoadRolesAsync(Organization.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)OrganizationRoles.Where(role => ids.Contains(role.Id)).ToList();
                });

            AuditTrail.When(trail => trail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Recorded.Add(callInfo.Arg<AuditEvent>()!));
            EmailSender.When(sender => sender.SendAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
                .Do(callInfo =>
                {
                    MailedInsideTransaction = inTransaction;
                    Mailed.Add((callInfo.ArgAt<string>(0), callInfo.ArgAt<string>(1), callInfo.ArgAt<string>(2)));
                });

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
                Substitute.For<IInvitationQueries>(),
                AuditTrail,
                EmailSender,
                Clock,
                Options.Create(new EmailSettings { FrontendBaseUrl = "https://hub.example.com/" }),
                Substitute.For<ITenantAccessService>(),
                Substitute.For<IPermissionResolver>(),
                Logger);
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

        public IOrganizationLock OrganizationLock { get; } = Substitute.For<IOrganizationLock>();

        public IAuditTrail AuditTrail { get; } = Substitute.For<IAuditTrail>();

        public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

        public IDateTimeProvider Clock { get; } = Substitute.For<IDateTimeProvider>();

        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

        public ICurrentTenantProvider Tenant { get; } = Substitute.For<ICurrentTenantProvider>();

        public CapturingLogger Logger { get; } = new();

        public List<AuditEvent> Recorded { get; } = [];

        public List<(string To, string Subject, string Body)> Mailed { get; } = [];

        public IReadOnlyList<OrganizationInvitation> Stored => invitations;

        public bool ReadBeforeLock { get; private set; }

        public bool MailedInsideTransaction { get; private set; }

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

        public OrganizationMember AddMember(string email, bool active = true)
        {
            Account account = AddAccount(email);
            OrganizationMember member =
                OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, account.Id));
            if (!active)
            {
                member.Deactivate();
            }

            members.Add(member);
            return member;
        }

        public OrganizationInvitation AddInvitation(string email, RoleGrantRole role, DateTime issuedAt)
        {
            OrganizationInvitation invitation = OrganizationInvitation.Create(new OrganizationInvitationParams(
                Organization.Id, email, role.Id, TokenHash.Compute($"old-{Guid.NewGuid():N}"), issuedAt));
            invitations.Add(invitation);
            return invitation;
        }

        public void DeactivateCaller() => members.Single(member => member.AccountId == CallerId).Deactivate();

        public Task<InvitationDto> SendAsync(string email, RoleGrantRole role) => SendAsync(email, role.Id);

        public Task<InvitationDto> SendAsync(string email, Guid roleId) =>
            Service.SendAsync(new SendInvitationRequest(email, roleId), CancellationToken.None);

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
            inTransaction = true;
            try
            {
                await operation(token);
            }
            catch (Exception exception)
            {
                ThrownFromTransaction = exception;
                throw;
            }
            finally
            {
                inTransaction = false;
            }
        }
    }

    // Keeps each entry's text, structured values and exception, to prove what a log line may carry.
    private sealed class CapturingLogger : ILogger<InvitationService>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : string.Empty;
            Entries.Add($"{formatter(state, exception)}|{values}|{exception}");
        }
    }

    private static Fixture CreateFixture() => new();

    private static string TokenIn(string body) => InviteLink().Match(body).Groups[1].Value;

    [GeneratedRegex("#/invite\\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    // Prelude: lock, gate re-check and organizations.members.manage from the database (C1, F2)

    [Fact]
    public async Task SendAsync_Any_AcquiresLockBeforeAnyRead()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddInvitation("b@x.com", TesterRole, Now.AddDays(-1));

        await f.SendAsync("b@x.com", MemberRole);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Assert.False(f.ReadBeforeLock);
    }

    // Removed between the gate and the lock: refused the way the gate now would, nothing written or mailed.
    [Fact]
    public async Task SendAsync_CallerNoLongerActiveMemberAfterGate_Throws403AccessDenied()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.DeactivateCaller();

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.SendAsync("b@x.com", MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task SendAsync_OrganizationSuspendedAfterGate_Throws403Suspended()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, Now));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationSuspended, () => f.SendAsync("b@x.com", MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    // members.manage lost after the gate (stale cache, or removed while waiting for the lock); BR-58 alone would pass.
    [Fact]
    public async Task SendAsync_CallerLostMembersManageAfterGate_Throws403AccessDenied()
    {
        Fixture f = CreateFixture();
        f.SetCaller(TesterRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.SendAsync("b@x.com", MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    // Success

    [Fact]
    public async Task SendAsync_NewEmail_CreatesInvitationRecordsSentAndMailsLinkAfterSave()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);

        await f.SendAsync("b@x.com", PeopleRole);

        OrganizationInvitation invitation = Assert.Single(f.Stored);
        Assert.Equal(f.Organization.Id, invitation.OrganizationId);
        Assert.Equal("b@x.com", invitation.Email);
        Assert.Equal(PeopleRole.Id, invitation.RoleId);
        Assert.Equal(Now + TimeSpan.FromHours(168), invitation.ExpiresAt);

        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.InvitationSent, recorded.Type);
        Assert.Equal(ActorKind.Account, recorded.ActorKind);
        Assert.Equal(f.CallerId, recorded.ActorAccountId);
        Assert.Equal(f.Organization.Id, recorded.OrganizationId);
        Assert.Equal(invitation.Id, recorded.SubjectId);
        Assert.Equal(["email", "role"], recorded.Parameters.Keys.Order());
        Assert.Equal("b@x.com", recorded.Parameters["email"]);
        Assert.Equal(new AuditRoleRef(PeopleRole.Id, "Nhân sự", null), recorded.Parameters["role"]);

        (string to, string subject, string body) = Assert.Single(f.Mailed);
        Assert.Equal("b@x.com", to);
        Assert.Equal("Lời mời tham gia Bảo Minh trên FeedbackHub", subject);
        Assert.Contains("https://hub.example.com/#/invite?token=", body);
        string token = TokenIn(body);
        Assert.Equal(43, token.Length);
        Assert.Equal(TokenHash.Compute(token), invitation.TokenHash);
        Assert.Contains($"<strong>{WebUtility.HtmlEncode("Nhân sự")}</strong>", body);
        Assert.True(f.MailedInsideTransaction);

        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Reader.LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            f.EmailSender.SendAsync("b@x.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(SystemRoleKind.Owner, "Chủ sở hữu")]
    [InlineData(SystemRoleKind.Admin, "Admin")]
    [InlineData(SystemRoleKind.Member, "Member")]
    public async Task SendAsync_SystemRole_MailNamesRoleInVietnamese(SystemRoleKind kind, string expectedName)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);

        await f.SendAsync("b@x.com", OrganizationRoles.Single(role => role.SystemKind == kind));

        Assert.Contains($"<strong>{WebUtility.HtmlEncode(expectedName)}</strong>", Assert.Single(f.Mailed).Body);
    }

    // Review Focus 1: the address is matched after trimming and lower-casing, so the unique index never trips.
    [Fact]
    public async Task SendAsync_EmailWithCaseAndSpaces_ReplacesExistingInvitation()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        OrganizationInvitation old = f.AddInvitation("b@x.com", TesterRole, Now.AddDays(-1));

        await f.SendAsync(" B@X.com ", MemberRole);

        OrganizationInvitation invitation = Assert.Single(f.Stored);
        Assert.NotEqual(old.Id, invitation.Id);
        Assert.Equal("b@x.com", invitation.Email);
        f.Invitations.Received(1).Delete(old);
        Assert.Equal("b@x.com", Assert.Single(f.Mailed).To);
    }

    // AC-325: only the new invitation's InvitationSent; nothing for the one replaced.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_ExistingPendingOrExpired_DeletesOldNoAuditForOld(bool expired)
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        f.AddInvitation("c@x.com", MemberRole, expired ? Now.AddDays(-8) : Now.AddDays(-1));

        await f.SendAsync("c@x.com", MemberRole);

        OrganizationInvitation invitation = Assert.Single(f.Stored);
        AuditEvent recorded = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.InvitationSent, recorded.Type);
        Assert.Equal(invitation.Id, recorded.SubjectId);
    }

    [Fact]
    public async Task SendAsync_EmailIsInactiveMember_Succeeds()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddMember("left@x.com", active: false);

        await f.SendAsync("left@x.com", MemberRole);

        Assert.Single(f.Stored);
        Assert.Single(f.Mailed);
    }

    [Fact]
    public async Task SendAsync_ReturnsDtoWithFlagsFromPolicy()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);

        InvitationDto dto = await f.SendAsync(" B@x.com", PeopleRole);

        OrganizationInvitation invitation = Assert.Single(f.Stored);
        Assert.Equal(invitation.Id, dto.Id);
        Assert.Equal("b@x.com", dto.Email);
        Assert.Equal(new RoleRefDto(PeopleRole.Id, "Nhân sự", null), dto.Role);
        Assert.Equal(invitation.ExpiresAt, dto.ExpiresAt);
        Assert.Equal(InvitationStatus.Pending, dto.Status);
        Assert.True(dto.CanRevokeOrDelete);
        Assert.True(dto.CanResend);
        Assert.Null(dto.LockReason);
    }

    // 404: the role

    [Fact]
    public async Task SendAsync_RoleNotInOrganization_Throws404AndSendsNothing()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        Guid foreignRoleId = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Role>(foreignRoleId, () => f.SendAsync("b@x.com", foreignRoleId));

        await f.AssertNothingWrittenAsync();
    }

    // F18: a body without roleId binds Guid.Empty, which is no role of the organization.
    [Fact]
    public async Task SendAsync_EmptyRoleId_Throws404()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);

        await ApplicationAssert.AssertNotFoundAsync<Role>(Guid.Empty, () => f.SendAsync("b@x.com", Guid.Empty));

        await f.AssertNothingWrittenAsync();
    }

    // 403: BR-58

    // AC-153 (Admin holds feedbackhub.admin the caller lacks), AC-194 (Owner role needs an Owner).
    [Theory]
    [InlineData("people-admin")]
    [InlineData("admin-owner")]
    public async Task SendAsync_CannotGrantNewRole_Throws403WithPolicyCode(string scenario)
    {
        Fixture f = CreateFixture();
        (RoleGrantRole callerRole, RoleGrantRole invited, string code) = scenario == "people-admin"
            ? (PeopleRole, AdminRole, ApplicationMessages.InsufficientRolePermissions)
            : (AdminRole, OwnerRole, ApplicationMessages.OwnerOnlyOperation);
        f.SetCaller(callerRole);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(code, () => f.SendAsync("b@x.com", invited));

        await f.AssertNothingWrittenAsync();
        Assert.Empty(f.Stored);
    }

    // BR-75, D4, AC-254, AC-271: replacing takes the old invitation's role away, so it must be within reach too.
    [Fact]
    public async Task SendAsync_CanGrantNewButCannotTakeAwayOldInvitationRole_Throws403OldKept()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation old = f.AddInvitation("b@x.com", ItLeadRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.SendAsync("b@x.com", MemberRole));

        await f.AssertNothingWrittenAsync();
        Assert.Same(old, Assert.Single(f.Stored));
    }

    // F3, TECH00 §5.3, AC-341: an old Owner invitation answers OwnerOnlyOperation even when the new role is also
    // out of reach.
    [Fact]
    public async Task SendAsync_OldInvitationOwnerAndNewRoleBeyondCaller_Throws403OwnerOnly()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        OrganizationInvitation old = f.AddInvitation("b@x.com", OwnerRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OwnerOnlyOperation, () => f.SendAsync("b@x.com", ItLeadRole));

        await f.AssertNothingWrittenAsync();
        Assert.Same(old, Assert.Single(f.Stored));
    }

    // 409: already a member

    // AC-180
    [Theory]
    [InlineData("m@congty.vn")]
    [InlineData(" M@CongTy.vn ")]
    public async Task SendAsync_EmailIsActiveMember_Throws409AlreadyMember(string typed)
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddMember("m@congty.vn");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationEmailAlreadyMember, () => f.SendAsync(typed, MemberRole));

        await f.AssertNothingWrittenAsync();
    }

    // D2: BR-58 (step 6) answers before the 409 (step 7).
    [Fact]
    public async Task SendAsync_EmailIsActiveMemberAndCannotGrant_Throws403Not409()
    {
        Fixture f = CreateFixture();
        f.SetCaller(PeopleRole);
        f.AddMember("m@congty.vn");

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.InsufficientRolePermissions, () => f.SendAsync("m@congty.vn", AdminRole));

        await f.AssertNothingWrittenAsync();
    }

    // Mail failure

    // Review Focus 2, F14, F17: the 503 leaves the transaction delegate so nothing commits; the log names only the id.
    [Fact]
    public async Task SendAsync_SmtpFails_Throws503FromInsideTransactionLogsOnlyInvitationId()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddInvitation("b@x.com", TesterRole, Now.AddDays(-1));
        string? token = null;
        f.EmailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                token = TokenIn(callInfo.ArgAt<string>(2));
                return Task.FromException(new ServiceUnavailableException(ApplicationMessages.EmailDeliveryFailed));
            });

        ServiceUnavailableException exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.SendAsync("b@x.com", MemberRole));

        Assert.Equal(ApplicationMessages.InvitationEmailDeliveryFailed, exception.Message);
        Assert.Same(exception, f.ThrownFromTransaction);
        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            f.EmailSender.SendAsync("b@x.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });

        OrganizationInvitation created = f.Stored.Single(invitation => invitation.RoleId == MemberRole.Id);
        Assert.NotNull(token);
        string entry = Assert.Single(f.Logger.Entries);
        Assert.Contains($"Failed to send invitation {created.Id}", entry);
        Assert.DoesNotContain("b@x.com", entry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, entry);
        Assert.DoesNotContain(created.TokenHash, entry);
        Assert.DoesNotContain("b@x.com", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, exception.ToString());
    }

    [Fact]
    public async Task SendAsync_CallerCancels_PropagatesOperationCanceled()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.EmailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => f.SendAsync("b@x.com", MemberRole));

        Assert.Empty(f.Logger.Entries);
    }

    // F17: refusals carry codes and ids only, never the address.
    [Fact]
    public async Task SendAsync_Refused_ExceptionNeverCarriesEmail()
    {
        Fixture f = CreateFixture();
        f.SetCaller(OwnerRole);
        f.AddMember("m@congty.vn");

        ConflictException exception =
            await Assert.ThrowsAsync<ConflictException>(() => f.SendAsync("m@congty.vn", MemberRole));

        Assert.DoesNotContain("m@congty.vn", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(f.Logger.Entries);
    }
}
