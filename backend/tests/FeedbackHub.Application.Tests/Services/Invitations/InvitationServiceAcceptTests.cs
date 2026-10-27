using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Invitations;

public class InvitationServiceAcceptTests
{
    private const string InvitedEmail = "bao@example.com";

    private static readonly DateTime Now = new(2026, 10, 23, 17, 30, 0, DateTimeKind.Utc);

    private static readonly RoleGrantRole MemberRole =
        new(Guid.NewGuid(), "Member", SystemRoleKind.Member, new HashSet<string>());

    private static readonly RoleGrantRole PeopleRole =
        new(Guid.NewGuid(), "Nhân sự", null, new HashSet<string> { Permissions.OrganizationMembersManage });

    private static readonly RoleGrantRole[] OrganizationRoles = [MemberRole, PeopleRole];

    public enum InvitationState
    {
        Missing,
        Pending,
        Expired
    }

    public enum SessionState
    {
        None,
        ApiKey,
        MatchingEmail,
        MatchingEmailUnconfirmed,
        OtherEmail
    }

    // TECH00 §6.3: one row per step, so a check moved earlier or later answers a different status.
    public static TheoryData<InvitationState, SessionState, bool, InvitationLookupStatus> LookupCases => new()
    {
        { InvitationState.Missing, SessionState.None, true, InvitationLookupStatus.NoLongerValid },
        { InvitationState.Missing, SessionState.MatchingEmail, true, InvitationLookupStatus.NoLongerValid },
        { InvitationState.Expired, SessionState.None, true, InvitationLookupStatus.Expired },
        { InvitationState.Expired, SessionState.OtherEmail, false, InvitationLookupStatus.Expired },
        { InvitationState.Expired, SessionState.MatchingEmailUnconfirmed, false, InvitationLookupStatus.Expired },
        { InvitationState.Pending, SessionState.OtherEmail, true, InvitationLookupStatus.WrongAccount },
        { InvitationState.Pending, SessionState.OtherEmail, false, InvitationLookupStatus.WrongAccount },
        {
            InvitationState.Pending, SessionState.MatchingEmailUnconfirmed, true,
            InvitationLookupStatus.EmailNotConfirmed
        },
        {
            InvitationState.Pending, SessionState.MatchingEmailUnconfirmed, false,
            InvitationLookupStatus.EmailNotConfirmed
        },
        { InvitationState.Pending, SessionState.MatchingEmail, false, InvitationLookupStatus.OrganizationSuspended },
        { InvitationState.Pending, SessionState.MatchingEmail, true, InvitationLookupStatus.Valid },
        { InvitationState.Pending, SessionState.None, true, InvitationLookupStatus.Valid },
        { InvitationState.Pending, SessionState.None, false, InvitationLookupStatus.Valid },
        { InvitationState.Pending, SessionState.ApiKey, false, InvitationLookupStatus.Valid }
    };

    public static TheoryData<InvitationState, SessionState, bool, Type, string> AcceptCases => new()
    {
        {
            InvitationState.Missing, SessionState.MatchingEmail, true, typeof(ConflictException),
            ApplicationMessages.InvitationNoLongerValid
        },
        {
            InvitationState.Expired, SessionState.MatchingEmail, true, typeof(ConflictException),
            ApplicationMessages.InvitationExpired
        },
        {
            InvitationState.Expired, SessionState.OtherEmail, false, typeof(ConflictException),
            ApplicationMessages.InvitationExpired
        },
        {
            InvitationState.Pending, SessionState.OtherEmail, true, typeof(ConflictException),
            ApplicationMessages.InvitationWrongAccount
        },
        {
            InvitationState.Pending, SessionState.OtherEmail, false, typeof(ConflictException),
            ApplicationMessages.InvitationWrongAccount
        },
        {
            InvitationState.Pending, SessionState.MatchingEmailUnconfirmed, true, typeof(ForbiddenException),
            ApplicationMessages.EmailNotConfirmed
        },
        {
            InvitationState.Pending, SessionState.MatchingEmailUnconfirmed, false, typeof(ForbiddenException),
            ApplicationMessages.EmailNotConfirmed
        },
        {
            InvitationState.Pending, SessionState.MatchingEmail, false, typeof(ConflictException),
            ApplicationMessages.InvitationOrganizationSuspended
        }
    };

    // An in-memory organization behind substitutes; OnLock runs while the lock is taken, as a concurrent request would.
    private sealed record Fixture
    {
        private readonly List<Organization> organizations = [];
        private readonly List<Account> accounts = [];
        private readonly List<OrganizationMember> members = [];
        private readonly List<OrganizationMemberRole> memberRoles = [];
        private readonly List<OrganizationInvitation> invitations = [];
        private bool inTransaction;

        public Fixture()
        {
            Organization = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));
            organizations.Add(Organization);

            UnitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => RunInTransactionAsync(
                    callInfo.Arg<Func<CancellationToken, Task>>()!, callInfo.Arg<CancellationToken>()));
            UnitOfWork.Repository<Organization, Guid>().Returns(Organizations);
            UnitOfWork.Repository<Account, Guid>().Returns(Accounts);
            UnitOfWork.Repository<OrganizationMember, Guid>().Returns(Members);
            UnitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(MemberRoles);
            UnitOfWork.Repository<OrganizationInvitation, Guid>().Returns(Invitations);

            Organizations.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => organizations.SingleOrDefault(o => o.Id == callInfo.Arg<Guid>()));
            Accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => accounts.SingleOrDefault(a => a.Id == callInfo.Arg<Guid>()));
            RepositoryPredicateStub.StubFirstOrDefault(Accounts, accounts);
            RepositoryPredicateStub.StubFirstOrDefault(Members, members);
            RepositoryPredicateStub.StubListAsync(MemberRoles, memberRoles);
            RepositoryPredicateStub.StubFirstOrDefault(Invitations, invitations);

            Members.When(repository => repository.AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => members.Add(callInfo.Arg<OrganizationMember>()!));
            MemberRoles.When(repository => repository.AddAsync(
                    Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => memberRoles.Add(callInfo.Arg<OrganizationMemberRole>()!));
            MemberRoles.When(repository => repository.Delete(Arg.Any<OrganizationMemberRole>()))
                .Do(callInfo => memberRoles.Remove(callInfo.Arg<OrganizationMemberRole>()!));
            Invitations.When(repository => repository.Delete(Arg.Any<OrganizationInvitation>()))
                .Do(callInfo => invitations.Remove(callInfo.Arg<OrganizationInvitation>()!));

            Reader.LoadRolesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)OrganizationRoles
                        .Where(role => callInfo.ArgAt<Guid>(0) == Organization.Id
                            && ids.Contains(role.Id) && !GoneRoleIds.Contains(role.Id))
                        .ToList();
                });
            TenantAccess.When(service => service.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => InvalidatedInsideTransaction |= inTransaction);
            PermissionResolver.When(resolver => resolver.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => InvalidatedInsideTransaction |= inTransaction);
            AuditTrail.When(trail => trail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => Recorded.Add(callInfo.Arg<AuditEvent>()!));

            OrganizationLock.When(organizationLock => organizationLock.AcquireAsync(
                    Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ =>
                {
                    InvitationReadsBeforeLock = Invitations.ReceivedCalls().Count();
                    OtherReadsBeforeLock = Reader.ReceivedCalls().Count() + Accounts.ReceivedCalls().Count()
                        + Members.ReceivedCalls().Count() + MemberRoles.ReceivedCalls().Count()
                        + Organizations.ReceivedCalls().Count();
                    OnLock?.Invoke();
                });

            Clock.UtcNow.Returns(_ => Now);
            // The working organization of the JWT is never where the invitation leads (A1).
            Tenant.OrganizationId.Returns(Guid.NewGuid());

            Service = new InvitationService(
                CurrentUser,
                Tenant,
                UnitOfWork,
                OrganizationLock,
                Reader,
                Substitute.For<IInvitationQueries>(),
                AuditTrail,
                Substitute.For<IEmailSender>(),
                Clock,
                Options.Create(new EmailSettings { FrontendBaseUrl = "https://hub.example.com/" }),
                TenantAccess,
                PermissionResolver,
                Substitute.For<ITokenIssuer>(),
                Substitute.For<IPasswordHasher>(),
                Substitute.For<ILogger<InvitationService>>());
        }

        public Organization Organization { get; }

        public InvitationService Service { get; }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IRepository<Organization, Guid> Organizations { get; } = Substitute.For<IRepository<Organization, Guid>>();

        public IRepository<Account, Guid> Accounts { get; } = Substitute.For<IRepository<Account, Guid>>();

        public IRepository<OrganizationMember, Guid> Members { get; } =
            Substitute.For<IRepository<OrganizationMember, Guid>>();

        public IRepository<OrganizationMemberRole, Guid> MemberRoles { get; } =
            Substitute.For<IRepository<OrganizationMemberRole, Guid>>();

        public IRepository<OrganizationInvitation, Guid> Invitations { get; } =
            Substitute.For<IRepository<OrganizationInvitation, Guid>>();

        public IRoleGrantReader Reader { get; } = Substitute.For<IRoleGrantReader>();

        public IOrganizationLock OrganizationLock { get; } = Substitute.For<IOrganizationLock>();

        public IAuditTrail AuditTrail { get; } = Substitute.For<IAuditTrail>();

        public IDateTimeProvider Clock { get; } = Substitute.For<IDateTimeProvider>();

        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

        public ICurrentTenantProvider Tenant { get; } = Substitute.For<ICurrentTenantProvider>();

        public ITenantAccessService TenantAccess { get; } = Substitute.For<ITenantAccessService>();

        public IPermissionResolver PermissionResolver { get; } = Substitute.For<IPermissionResolver>();

        public List<AuditEvent> Recorded { get; } = [];

        public HashSet<Guid> GoneRoleIds { get; } = [];

        public Action? OnLock { get; set; }

        public bool InvalidatedInsideTransaction { get; private set; }

        public int InvitationReadsBeforeLock { get; private set; } = -1;

        public int OtherReadsBeforeLock { get; private set; } = -1;

        public IReadOnlyList<OrganizationInvitation> StoredInvitations => invitations;

        public IReadOnlyList<OrganizationMember> StoredMembers => members;

        public IReadOnlyList<Guid> RoleIdsOf(OrganizationMember member) =>
            memberRoles.Where(link => link.OrganizationMemberId == member.Id).Select(link => link.RoleId).ToList();

        public Account AddAccount(string email, bool confirmed = true)
        {
            string handle = $"u{Guid.NewGuid():N}"[..12];
            Account account = Account.Create(new AccountParams("Trần Bảo", handle, email));
            if (confirmed)
            {
                account.ConfirmEmail();
            }

            accounts.Add(account);
            return account;
        }

        public Account SignIn(string email, bool confirmed = true)
        {
            Account account = AddAccount(email, confirmed);
            CurrentUser.UserId.Returns(account.Id.ToString());
            return account;
        }

        public void SignInWithApiKey(Account onBehalfOf)
        {
            CurrentUser.UserId.Returns(onBehalfOf.Id.ToString());
            CurrentUser.IsApiKeyCaller.Returns(true);
        }

        public OrganizationMember AddMember(Account account, bool active, params RoleGrantRole[] roles)
        {
            OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(Organization.Id, account.Id));
            if (!active)
            {
                member.Deactivate();
            }

            members.Add(member);
            memberRoles.AddRange(roles.Select(role =>
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id))));
            return member;
        }

        public (string Token, OrganizationInvitation Invitation) Invite(
            string email, RoleGrantRole role, DateTime issuedAt)
        {
            string token = $"tok-{Guid.NewGuid():N}";
            OrganizationInvitation invitation = OrganizationInvitation.Create(new OrganizationInvitationParams(
                Organization.Id, email, role.Id, TokenHash.Compute(token), issuedAt));
            invitations.Add(invitation);
            return (token, invitation);
        }

        public void Suspend() => Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, Now));

        public (string Token, OrganizationInvitation? Invitation) Arrange(
            InvitationState invitationState, SessionState session, bool organizationActive)
        {
            switch (session)
            {
                case SessionState.MatchingEmail:
                    SignIn(InvitedEmail);
                    break;
                case SessionState.MatchingEmailUnconfirmed:
                    SignIn(InvitedEmail, confirmed: false);
                    break;
                case SessionState.OtherEmail:
                    // Unconfirmed too, so a check for EmailNotConfirmed placed first would answer that instead.
                    SignIn("someone-else@example.com", confirmed: false);
                    break;
                case SessionState.ApiKey:
                    SignInWithApiKey(AddAccount("key-owner@example.com"));
                    break;
            }

            if (!organizationActive)
            {
                Suspend();
            }

            return invitationState switch
            {
                InvitationState.Missing => ($"tok-{Guid.NewGuid():N}", null),
                // Exactly at ExpiresAt counts as expired (BR-24).
                InvitationState.Expired => Invite(InvitedEmail, MemberRole, Now - OrganizationInvitation.Lifetime),
                _ => Invite(InvitedEmail, MemberRole, Now.AddDays(-1))
            };
        }

        public Task<InvitationLookupDto> LookupAsync(string token) =>
            Service.LookupAsync(new InvitationTokenRequest(token), CancellationToken.None);

        public Task<AcceptInvitationResult> AcceptAsync(string token) =>
            Service.AcceptAsync(new InvitationTokenRequest(token), CancellationToken.None);

        public async Task AssertNothingWrittenAsync()
        {
            await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            Invitations.DidNotReceive().Delete(Arg.Any<OrganizationInvitation>());
            await Members.DidNotReceive().AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
            Members.DidNotReceive().Update(Arg.Any<OrganizationMember>());
            await MemberRoles.DidNotReceive().AddAsync(Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>());
            await AssertCachesKeptAsync();
        }

        public async Task AssertCachesKeptAsync()
        {
            await TenantAccess.DidNotReceive().InvalidateMemberAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await PermissionResolver.DidNotReceive().InvalidateMemberAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        private async Task RunInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            inTransaction = true;
            try
            {
                await operation(token);
            }
            finally
            {
                inTransaction = false;
            }
        }
    }

    private static Fixture CreateFixture() => new();

    private static InvitationLookupDto ExpectedLookup(
        InvitationLookupStatus status, Organization organization, OrganizationInvitation? invitation) => status switch
    {
        InvitationLookupStatus.Valid => new InvitationLookupDto(
            status,
            organization.Name,
            new RoleRefDto(MemberRole.Id, MemberRole.Name, MemberRole.SystemKind),
            invitation?.ExpiresAt,
            invitation?.Email),
        InvitationLookupStatus.Expired => new InvitationLookupDto(status, null, null, invitation?.ExpiresAt, null),
        InvitationLookupStatus.OrganizationSuspended =>
            new InvitationLookupDto(status, organization.Name, null, invitation?.ExpiresAt, null),
        _ => new InvitationLookupDto(status, null, null, null, null)
    };

    // LookupAsync

    // D10, AC-306, AC-308, AC-309, AC-332, AC-351: each status carries only the fields §6.3 allows it.
    [Theory]
    [MemberData(nameof(LookupCases))]
    public async Task LookupAsync_Br81Order_ReturnsStatusWithOnlyAllowedFields(
        InvitationState invitationState, SessionState session, bool organizationActive, InvitationLookupStatus expected)
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation? invitation) = f.Arrange(invitationState, session, organizationActive);

        InvitationLookupDto result = await f.LookupAsync(token);

        Assert.Equal(ExpectedLookup(expected, f.Organization, invitation), result);
    }

    [Fact]
    public async Task LookupAsync_WrongAccountCheckedBeforeEmailNotConfirmed_ReturnsWrongAccount()
    {
        Fixture f = CreateFixture();
        f.SignIn("other@example.com", confirmed: false);
        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        InvitationLookupDto result = await f.LookupAsync(token);

        Assert.Equal(InvitationLookupStatus.WrongAccount, result.Status);
    }

    // AC-348, BR-51: an email change not yet confirmed does not make the account the invited one.
    [Fact]
    public async Task LookupAsync_AccountWhosePendingEmailMatches_ReturnsWrongAccount()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn("old@example.com");
        account.RequestEmailChange(InvitedEmail);
        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        InvitationLookupDto result = await f.LookupAsync(token);

        Assert.Equal(new InvitationLookupDto(InvitationLookupStatus.WrongAccount, null, null, null, null), result);
    }

    // A mail scanner opening the link must not use it up (TECH00 §6.3).
    [Fact]
    public async Task LookupAsync_CalledTwice_ReturnsValidBothTimesAndWritesNothing()
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        InvitationLookupDto first = await f.LookupAsync(token);
        InvitationLookupDto second = await f.LookupAsync(token);

        Assert.Equal(InvitationLookupStatus.Valid, first.Status);
        Assert.Equal(first, second);
        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
        await f.AssertNothingWrittenAsync();
        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    // A role deleted between the two reads took the invitation with it (ON DELETE CASCADE).
    [Fact]
    public async Task LookupAsync_RoleGoneAfterInvitationRead_ReturnsNoLongerValid()
    {
        Fixture f = CreateFixture();
        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));
        f.GoneRoleIds.Add(MemberRole.Id);

        InvitationLookupDto result = await f.LookupAsync(token);

        Assert.Equal(new InvitationLookupDto(InvitationLookupStatus.NoLongerValid, null, null, null, null), result);
    }

    // AcceptAsync

    // AC-151, AC-305, AC-351: refused in the BR-81 order, with nothing written and the invitation kept.
    [Theory]
    [MemberData(nameof(AcceptCases))]
    public async Task AcceptAsync_Br81Order_ThrowsMatchingCode(
        InvitationState invitationState,
        SessionState session,
        bool organizationActive,
        Type exceptionType,
        string code)
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation? invitation) = f.Arrange(invitationState, session, organizationActive);

        AppException refused = await Assert.ThrowsAnyAsync<AppException>(() => f.AcceptAsync(token));

        Assert.Equal(exceptionType, refused.GetType());
        Assert.Equal(code, refused.Message);
        Assert.Equal(invitation is null ? [] : [invitation], f.StoredInvitations);
        Assert.Empty(f.StoredMembers);
        await f.AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptAsync_NoSessionOrApiKey_Throws401(bool apiKey)
    {
        Fixture f = CreateFixture();
        if (apiKey)
        {
            f.SignInWithApiKey(f.AddAccount(InvitedEmail));
        }

        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired, () => f.AcceptAsync(token));

        Assert.Single(f.StoredInvitations);
        await f.AssertNothingWrittenAsync();
    }

    // AC-57, AC-374: the member row, the invitation's role, the entry in the invitation's organization, then caches.
    [Fact]
    public async Task AcceptAsync_Valid_JoinsWithInvitationRoleDeletesInvitationRecordsMemberJoined()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn(InvitedEmail);
        (string token, _) = f.Invite(InvitedEmail, PeopleRole, Now.AddDays(-1));

        AcceptInvitationResult result = await f.AcceptAsync(token);

        Assert.Equal(new AcceptInvitationResult(f.Organization.Id, f.Organization.Name), result);
        Assert.Empty(f.StoredInvitations);
        OrganizationMember member = Assert.Single(f.StoredMembers);
        Assert.Equal((f.Organization.Id, account.Id, true), (member.OrganizationId, member.AccountId, member.IsActive));
        Assert.Equal([PeopleRole.Id], f.RoleIdsOf(member));

        AuditEvent joined = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.MemberJoined, joined.Type);
        Assert.Equal(f.Organization.Id, joined.OrganizationId);
        Assert.Equal(member.Id, joined.SubjectId);
        Assert.Equal(ActorKind.Account, joined.ActorKind);
        Assert.Equal(account.Id, joined.ActorAccountId);
        Assert.Equal(
            new Dictionary<string, object>
            {
                ["accountName"] = account.Name,
                ["email"] = InvitedEmail,
                ["role"] = new AuditRoleRef(PeopleRole.Id, PeopleRole.Name, null)
            },
            joined.Parameters);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            f.TenantAccess.InvalidateMemberAsync(f.Organization.Id, account.Id, Arg.Any<CancellationToken>());
            f.PermissionResolver.InvalidateMemberAsync(f.Organization.Id, account.Id, Arg.Any<CancellationToken>());
        });
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.False(f.InvalidatedInsideTransaction);
    }

    // The invitation's organization id is the only thing read before the lock; the rest is read under it (F9).
    [Fact]
    public async Task AcceptAsync_Valid_ReadsOnlyTheInvitationBeforeTheLock()
    {
        Fixture f = CreateFixture();
        f.SignIn(InvitedEmail);
        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        await f.AcceptAsync(token);

        Assert.Equal(1, f.InvitationReadsBeforeLock);
        Assert.Equal(0, f.OtherReadsBeforeLock);
    }

    // QĐ-16, BR-30: a former member comes back on the same row, holding exactly the invitation's role.
    [Fact]
    public async Task AcceptAsync_FormerMember_ReactivatesSameRowWithOnlyInvitationRole()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn(InvitedEmail);
        OrganizationMember former = f.AddMember(account, active: false, PeopleRole, MemberRole);
        (string token, _) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        await f.AcceptAsync(token);

        Assert.Same(former, Assert.Single(f.StoredMembers));
        Assert.True(former.IsActive);
        f.Members.Received(1).Update(former);
        await f.Members.DidNotReceive().AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
        Assert.Equal([MemberRole.Id], f.RoleIdsOf(former));
        Assert.Equal(former.Id, Assert.Single(f.Recorded).SubjectId);
        Assert.Empty(f.StoredInvitations);
    }

    // Review Focus 1: addresses compare in their normalized form.
    [Fact]
    public async Task AcceptAsync_EmailWithDifferentCase_Joins()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn("  Bao@Example.COM ");
        (string token, _) = f.Invite("BAO@example.com", MemberRole, Now.AddDays(-1));

        await f.AcceptAsync(token);

        Assert.Equal(account.Id, Assert.Single(f.StoredMembers).AccountId);
    }

    // TECH00 §6.3: already an active member (a second tab) uses the invitation up and changes nothing else.
    [Fact]
    public async Task AcceptAsync_AlreadyActiveMember_DeletesInvitationNoRoleChangeNoAudit()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn(InvitedEmail);
        OrganizationMember member = f.AddMember(account, active: true, PeopleRole);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));

        AcceptInvitationResult result = await f.AcceptAsync(token);

        Assert.Equal(new AcceptInvitationResult(f.Organization.Id, f.Organization.Name), result);
        f.Invitations.Received(1).Delete(invitation);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Empty(f.StoredInvitations);
        Assert.Equal([PeopleRole.Id], f.RoleIdsOf(member));
        Assert.Empty(f.Recorded);
        await f.AssertCachesKeptAsync();
    }

    // The BR-81 checks come before the already-a-member shortcut, so they keep the invitation for members too.
    [Theory]
    [InlineData(true, false, ApplicationMessages.InvitationExpired)]
    [InlineData(false, true, ApplicationMessages.InvitationOrganizationSuspended)]
    public async Task AcceptAsync_AlreadyActiveMemberAndCheckFails_ThrowsAndKeepsInvitation(
        bool expired, bool suspended, string code)
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn(InvitedEmail);
        f.AddMember(account, active: true, PeopleRole);
        if (suspended)
        {
            f.Suspend();
        }

        DateTime issuedAt = expired ? Now - OrganizationInvitation.Lifetime : Now.AddDays(-1);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, issuedAt);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(code, () => f.AcceptAsync(token));

        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
        await f.AssertNothingWrittenAsync();
    }

    // Review Focus 3: the other tab joined and used the invitation up while this one waited for the lock.
    [Fact]
    public async Task AcceptAsync_InvitationGoneAfterLockAndCallerIsActiveMember_Returns200()
    {
        Fixture f = CreateFixture();
        Account account = f.SignIn(InvitedEmail);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));
        f.OnLock = () =>
        {
            f.AddMember(account, active: true, MemberRole);
            f.Invitations.Delete(invitation);
            f.Invitations.ClearReceivedCalls();
            f.Members.ClearReceivedCalls();
        };

        AcceptInvitationResult result = await f.AcceptAsync(token);

        Assert.Equal(new AcceptInvitationResult(f.Organization.Id, f.Organization.Name), result);
        Assert.Single(f.StoredMembers);
        await f.AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task AcceptAsync_InvitationGoneAfterLockAndCallerNotMember_Throws409NoLongerValid()
    {
        Fixture f = CreateFixture();
        f.SignIn(InvitedEmail);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));
        f.OnLock = () =>
        {
            f.Invitations.Delete(invitation);
            f.Invitations.ClearReceivedCalls();
        };

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationNoLongerValid, () => f.AcceptAsync(token));

        Assert.Empty(f.StoredMembers);
        await f.AssertNothingWrittenAsync();
    }

    // Review Focus 5, AC-181: deleting the role cascaded to its invitations, so the code finds nothing.
    [Fact]
    public async Task AcceptAsync_RoleDeleted_Throws409NoLongerValid()
    {
        Fixture f = CreateFixture();
        f.SignIn(InvitedEmail);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, MemberRole, Now.AddDays(-1));
        f.GoneRoleIds.Add(MemberRole.Id);
        f.Invitations.Delete(invitation);
        f.Invitations.ClearReceivedCalls();

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationNoLongerValid, () => f.AcceptAsync(token));

        await f.AssertNothingWrittenAsync();
    }

    // F17: refusals carry the code only, never the token, its hash or an address.
    [Theory]
    [MemberData(nameof(AcceptCases))]
    public async Task AcceptAsync_Refused_ExceptionCarriesNoTokenHashOrEmail(
        InvitationState invitationState,
        SessionState session,
        bool organizationActive,
        Type exceptionType,
        string code)
    {
        Fixture f = CreateFixture();
        (string token, _) = f.Arrange(invitationState, session, organizationActive);

        AppException refused = await Assert.ThrowsAnyAsync<AppException>(() => f.AcceptAsync(token));

        Assert.IsType(exceptionType, refused);
        Assert.Equal(code, refused.Message);
        string text = refused.ToString();
        Assert.DoesNotContain(token, text);
        Assert.DoesNotContain(TokenHash.Compute(token), text);
        Assert.DoesNotContain("@example.com", text);
    }
}
