using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Accounts;
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

public class InvitationServiceRegisterTests
{
    private const string InvitedEmail = "bao@example.com";
    private const string DeviceInfo = "Firefox on Linux";
    private const string IpAddress = "203.0.113.7";
    private const string PasswordHash = "hashed-password";

    private static readonly DateTime Now = new(2026, 10, 23, 17, 30, 0, DateTimeKind.Utc);

    private static readonly RoleGrantRole MemberRole =
        new(Guid.NewGuid(), "Member", SystemRoleKind.Member, new HashSet<string>());

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
            UnitOfWork.When(unitOfWork => unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()))
                .Do(_ => Steps.Add("save"));

            Organizations.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => organizations.SingleOrDefault(o => o.Id == callInfo.Arg<Guid>()));
            RepositoryPredicateStub.StubFirstOrDefault(Accounts, accounts);
            RepositoryPredicateStub.StubFirstOrDefault(Members, members);
            RepositoryPredicateStub.StubListAsync(MemberRoles, memberRoles);
            RepositoryPredicateStub.StubFirstOrDefault(Invitations, invitations);

            Accounts.When(repository => repository.AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => accounts.Add(callInfo.Arg<Account>()!));
            Members.When(repository => repository.AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => members.Add(callInfo.Arg<OrganizationMember>()!));
            MemberRoles.When(repository => repository.AddAsync(
                    Arg.Any<OrganizationMemberRole>(), Arg.Any<CancellationToken>()))
                .Do(callInfo => memberRoles.Add(callInfo.Arg<OrganizationMemberRole>()!));
            Invitations.When(repository => repository.Delete(Arg.Any<OrganizationInvitation>()))
                .Do(callInfo => invitations.Remove(callInfo.Arg<OrganizationInvitation>()!));

            Reader.LoadRolesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    IReadOnlyCollection<Guid> ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(1);
                    return (IReadOnlyList<RoleGrantRole>)new[] { MemberRole }
                        .Where(role => callInfo.ArgAt<Guid>(0) == Organization.Id && ids.Contains(role.Id))
                        .ToList();
                });
            TenantAccess.When(service => service.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => Steps.Add(inTransaction ? "invalidate-inside" : "invalidate"));
            PermissionResolver.When(resolver => resolver.InvalidateMemberAsync(
                    Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(_ => Steps.Add(inTransaction ? "invalidate-inside" : "invalidate"));
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

            PasswordHasher.Hash(Arg.Any<string>()).Returns(PasswordHash);
            TokenIssuer.IssueTokensAsync(
                    Arg.Any<Account>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(),
                    Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Steps.Add(inTransaction ? "issue-inside" : "issue");
                    Account account = callInfo.Arg<Account>()!;
                    return new AuthResult(
                        "access-token",
                        "refresh-token",
                        Now.AddMinutes(15),
                        new AccountDto(
                            account.Id, account.Name, null, null, null, true, account.Username, account.Email,
                            account.EmailConfirmed, Now, null),
                        false,
                        callInfo.ArgAt<Guid?>(1),
                        null,
                        [],
                        false);
                });

            Clock.UtcNow.Returns(_ => Now);
            // Registration is anonymous: whatever the request context says never decides the organization (A1).
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
                TokenIssuer,
                PasswordHasher,
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

        public ITokenIssuer TokenIssuer { get; } = Substitute.For<ITokenIssuer>();

        public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

        public List<AuditEvent> Recorded { get; } = [];

        // The order of save, cache invalidation and session issue, each marked "-inside" when inside the transaction.
        public List<string> Steps { get; } = [];

        public Action? OnLock { get; set; }

        public int InvitationReadsBeforeLock { get; private set; } = -1;

        public int OtherReadsBeforeLock { get; private set; } = -1;

        public IReadOnlyList<OrganizationInvitation> StoredInvitations => invitations;

        public IReadOnlyList<OrganizationMember> StoredMembers => members;

        public IReadOnlyList<Account> StoredAccounts => accounts;

        public IReadOnlyList<Guid> RoleIdsOf(OrganizationMember member) =>
            memberRoles.Where(link => link.OrganizationMemberId == member.Id).Select(link => link.RoleId).ToList();

        public Account AddAccount(string username, string email, bool confirmed = true)
        {
            Account account = Account.Create(new AccountParams("Người có sẵn", username, email));
            if (confirmed)
            {
                account.ConfirmEmail();
            }

            accounts.Add(account);
            return account;
        }

        // Returns the raw token of the link; only its hash is stored.
        public (string Token, OrganizationInvitation Invitation) Invite(string email, DateTime issuedAt)
        {
            string token = $"tok-{Guid.NewGuid():N}";
            OrganizationInvitation invitation = OrganizationInvitation.Create(new OrganizationInvitationParams(
                Organization.Id, email, MemberRole.Id, TokenHash.Compute(token), issuedAt));
            invitations.Add(invitation);
            return (token, invitation);
        }

        public void RemoveInvitation(OrganizationInvitation invitation) => invitations.Remove(invitation);

        public void Suspend() => Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, Now));

        public Task<RegisterWithInvitationResult> RegisterAsync(string token, string username = "tranbao") =>
            Service.RegisterAsync(
                new RegisterWithInvitationRequest(
                    token, "Trần Bảo", username, "Str0ng-password!", "0901234567", "Kế toán", "Hà Nội"),
                DeviceInfo,
                IpAddress,
                CancellationToken.None);

        public async Task AssertNothingCreatedAsync()
        {
            await Accounts.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
            await UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
            await AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            await Members.DidNotReceive().AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
            Invitations.DidNotReceive().Delete(Arg.Any<OrganizationInvitation>());
            await TokenIssuer.DidNotReceiveWithAnyArgs().IssueTokensAsync(
                default!, default, default, default, default, default, default, default);
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

    // AC-57, AC-182, BR-63 point 6: the link proves the address, so the account is verified and joins at once.
    [Fact]
    public async Task RegisterAsync_Valid_CreatesConfirmedAccountWithInvitationEmailJoinsAndIssuesSessionThroughTokenIssuer()
    {
        Fixture f = CreateFixture();
        (string token, _) = f.Invite(InvitedEmail, Now.AddDays(-1));

        RegisterWithInvitationResult result = await f.RegisterAsync(token);

        Account created = Assert.Single(f.StoredAccounts);
        Assert.Equal(
            (InvitedEmail, true, PasswordHash, "tranbao", "Trần Bảo"),
            (created.Email, created.EmailConfirmed, created.PasswordHash, created.Username, created.Name));
        Assert.Equal(("0901234567", "Kế toán", "Hà Nội"), (created.Phone, created.Position, created.Address));
        f.PasswordHasher.Received(1).Hash("Str0ng-password!");

        await f.TokenIssuer.Received(1).IssueTokensAsync(
            Arg.Is<Account>(account => account != null && account.Id == created.Id),
            f.Organization.Id,
            DeviceInfo,
            IpAddress,
            false,
            Now,
            null,
            Arg.Any<CancellationToken>());
        Assert.Equal(InvitationOutcome.Joined, result.Outcome);
        Assert.Equal(created.Id, result.Auth.Account.Id);
        Assert.Equal(f.Organization.Id, result.Auth.OrganizationId);

        Assert.Empty(f.StoredInvitations);
        OrganizationMember member = Assert.Single(f.StoredMembers);
        Assert.Equal((f.Organization.Id, created.Id, true), (member.OrganizationId, member.AccountId, member.IsActive));
        Assert.Equal([MemberRole.Id], f.RoleIdsOf(member));

        AuditEvent joined = Assert.Single(f.Recorded);
        Assert.Equal(AuditEventType.MemberJoined, joined.Type);
        Assert.Equal(f.Organization.Id, joined.OrganizationId);
        Assert.Equal(member.Id, joined.SubjectId);
        Assert.Equal(ActorKind.Account, joined.ActorKind);
        Assert.Equal(created.Id, joined.ActorAccountId);
        Assert.Equal(InvitedEmail, joined.Parameters["email"]);
    }

    // The session reads the permissions just saved, and no cache is filled from a transaction that may roll back.
    [Fact]
    public async Task RegisterAsync_Valid_SavesThenInvalidatesCachesThenIssuesSessionOutsideTransaction()
    {
        Fixture f = CreateFixture();
        (string token, _) = f.Invite(InvitedEmail, Now.AddDays(-1));

        RegisterWithInvitationResult result = await f.RegisterAsync(token);

        Assert.Equal(["save", "invalidate", "invalidate", "issue"], f.Steps);
        Guid accountId = result.Auth.Account.Id;
        await f.TenantAccess.Received(1).InvalidateMemberAsync(f.Organization.Id, accountId, Arg.Any<CancellationToken>());
        await f.PermissionResolver.Received(1).InvalidateMemberAsync(
            f.Organization.Id, accountId, Arg.Any<CancellationToken>());
    }

    // F9: the invitee is not a member, so the invitation's own organization is locked before anything else is read.
    [Fact]
    public async Task RegisterAsync_Valid_LocksInvitationOrganizationBeforeReadingAnythingElse()
    {
        Fixture f = CreateFixture();
        (string token, _) = f.Invite(InvitedEmail, Now.AddDays(-1));

        await f.RegisterAsync(token);

        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        Assert.Equal(1, f.InvitationReadsBeforeLock);
        Assert.Equal(0, f.OtherReadsBeforeLock);
    }

    // AC-333, BR-81 point 6: the account exists and is verified, but joins nothing and the invitation stays.
    [Fact]
    public async Task RegisterAsync_OrganizationSuspended_CreatesAccountNoJoinInvitationKeptSessionWithoutOrganization()
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, Now.AddDays(-1));
        f.Suspend();

        RegisterWithInvitationResult result = await f.RegisterAsync(token);

        Account created = Assert.Single(f.StoredAccounts);
        Assert.Equal((InvitedEmail, true), (created.Email, created.EmailConfirmed));
        Assert.Equal(InvitationOutcome.OrganizationSuspended, result.Outcome);
        await f.TokenIssuer.Received(1).IssueTokensAsync(
            Arg.Is<Account>(account => account != null && account.Id == created.Id),
            null,
            DeviceInfo,
            IpAddress,
            false,
            Now,
            null,
            Arg.Any<CancellationToken>());

        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
        f.Invitations.DidNotReceive().Delete(Arg.Any<OrganizationInvitation>());
        Assert.Empty(f.StoredMembers);
        Assert.Empty(f.Recorded);
        Assert.Equal(["save", "issue"], f.Steps);
    }

    [Fact]
    public async Task RegisterAsync_UnknownOrUsedToken_Throws409NoLongerValidCreatesNothing()
    {
        Fixture f = CreateFixture();
        f.Invite(InvitedEmail, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationNoLongerValid, () => f.RegisterAsync($"tok-{Guid.NewGuid():N}"));

        await f.AssertNothingCreatedAsync();
        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // F9: a second registration or an accept that used the link while this request waited for the lock.
    [Fact]
    public async Task RegisterAsync_InvitationGoneAfterLock_Throws409NoLongerValidCreatesNothing()
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, Now.AddDays(-1));
        f.OnLock = () => f.RemoveInvitation(invitation);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationNoLongerValid, () => f.RegisterAsync(token));

        await f.AssertNothingCreatedAsync();
    }

    // BR-24: exactly at ExpiresAt counts as expired.
    [Fact]
    public async Task RegisterAsync_Expired_Throws409ExpiredCreatesNothing()
    {
        Fixture f = CreateFixture();
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, Now - OrganizationInvitation.Lifetime);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationExpired, () => f.RegisterAsync(token));

        await f.AssertNothingCreatedAsync();
        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
    }

    // Expiry is checked before the account fields, so an expired link never reveals that a username is taken.
    [Fact]
    public async Task RegisterAsync_ExpiredAndUsernameTaken_Throws409Expired()
    {
        Fixture f = CreateFixture();
        f.AddAccount("tranbao", "someone@example.com");
        (string token, _) = f.Invite(InvitedEmail, Now - OrganizationInvitation.Lifetime);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.InvitationExpired, () => f.RegisterAsync(token));

        await f.AssertNothingCreatedAsync();
    }

    // Review Focus 4: the person retries with another username and the link still works.
    [Fact]
    public async Task RegisterAsync_UsernameTaken_Throws409InvitationKeptNoSession()
    {
        Fixture f = CreateFixture();
        f.AddAccount("tranbao", "someone@example.com");
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountUsernameAlreadyExists, () => f.RegisterAsync(token, username: "  TranBao "));

        await f.AssertNothingCreatedAsync();
        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
    }

    // TECH00 §6.3: an unverified account verifies its email and then accepts; it does not register again.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterAsync_EmailAlreadyHasAccount_Throws409AccountEmailAlreadyExists(bool confirmed)
    {
        Fixture f = CreateFixture();
        f.AddAccount("someone", InvitedEmail, confirmed);
        (string token, OrganizationInvitation invitation) = f.Invite(InvitedEmail, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists, () => f.RegisterAsync(token));

        await f.AssertNothingCreatedAsync();
        Assert.Same(invitation, Assert.Single(f.StoredInvitations));
    }

    // The username check comes first, as in plain registration.
    [Fact]
    public async Task RegisterAsync_UsernameAndEmailBothTaken_Throws409UsernameAlreadyExists()
    {
        Fixture f = CreateFixture();
        f.AddAccount("tranbao", InvitedEmail);
        (string token, _) = f.Invite(InvitedEmail, Now.AddDays(-1));

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountUsernameAlreadyExists, () => f.RegisterAsync(token));

        await f.AssertNothingCreatedAsync();
    }
}
