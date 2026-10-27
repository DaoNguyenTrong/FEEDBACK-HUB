using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Auth;

public class AuthServiceTests
{
    private const string GoogleProviderName = "Google";

    private sealed record Fixture(
        AuthService Service,
        IRepository<Account, Guid> AccountRepo,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IRepository<ExternalLogin, Guid> ExternalLoginRepo,
        IUnitOfWork UnitOfWork,
        IJwtTokenService JwtTokenService,
        ITenantAccessService TenantAccessService,
        IPasswordHasher PasswordHasher,
        IExternalAuthProvider ExternalAuthProvider,
        ICurrentUserService CurrentUserService,
        IRepository<OrganizationMember, Guid> OrganizationMemberRepo,
        IRepository<Organization, Guid> OrganizationRepo,
        IPasswordAttemptStore LoginAttempts);

    private static Fixture CreateFixture(
        int accessTokenExpiryMinutes = 15,
        int refreshTokenExpiryDays = 7,
        int refreshTokenReuseGraceSeconds = 60)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        IRepository<ExternalLogin, Guid> externalLoginRepo = Substitute.For<IRepository<ExternalLogin, Guid>>();
        IRepository<OrganizationMember, Guid> organizationMemberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        unitOfWork.Repository<ExternalLogin, Guid>().Returns(externalLoginRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(organizationMemberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        // Default: account belongs to no organization, matching pre-multi-tenant behavior
        // (tokens issued with no org claim) unless a test explicitly seeds memberships.
        organizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
        jwtTokenService.GenerateAccessToken(Arg.Any<Account>(), Arg.Any<Guid?>(), Arg.Any<Guid>()).Returns("fake-access-token");
        jwtTokenService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString());

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();
        tenantAccessService.GetAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.Active);

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        IExternalAuthProvider externalAuthProvider = Substitute.For<IExternalAuthProvider>();
        externalAuthProvider.ProviderName.Returns(GoogleProviderName);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        // Default: first attempt of a fresh window — tests about the lock seed their own count.
        IPasswordAttemptStore loginAttempts = Substitute.For<IPasswordAttemptStore>();
        loginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(1);

        IOptions<JwtSettings> jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = accessTokenExpiryMinutes,
            RefreshTokenExpiryDays = refreshTokenExpiryDays,
            RefreshTokenReuseGraceSeconds = refreshTokenReuseGraceSeconds
        });

        // TokenIssuer has no I/O beyond its substituted collaborators (IUnitOfWork, IJwtTokenService,
        // IPermissionResolver, ISystemAdministratorResolver) — wiring the real implementation here (rather than
        // mocking ITokenIssuer) preserves every pre-split assertion on token-issuing behavior unchanged.
        ITokenIssuer tokenIssuer = new TokenIssuer(
            unitOfWork,
            jwtTokenService,
            permissionResolver,
            Substitute.For<ISystemAdministratorResolver>(),
            jwtOptions,
            Options.Create(new StorageSettings()));

        AuthService service = new(
            unitOfWork,
            currentUserService,
            tokenIssuer,
            tenantAccessService,
            passwordHasher,
            [externalAuthProvider],
            loginAttempts,
            Options.Create(new LoginLockoutSettings { MaxFailedAttempts = 5, WindowMinutes = 15, LockDurationMinutes = 15 }),
            jwtOptions);

        return new Fixture(
            service,
            accountRepo,
            refreshTokenRepo,
            externalLoginRepo,
            unitOfWork,
            jwtTokenService,
            tenantAccessService,
            passwordHasher,
            externalAuthProvider,
            currentUserService,
            organizationMemberRepo,
            organizationRepo,
            loginAttempts);
    }

    private static Account CreateAccount(
        string username = "nva",
        string email = "nva@example.com",
        bool status = true,
        string? passwordHash = "hashed-password",
        bool emailConfirmed = true)
    {
        Account account = Account.Create(new AccountParams(
            Name: "Nguyen Van A",
            Username: username,
            Email: email));

        if (!status)
        {
            account.Deactivate();
        }

        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }

        if (emailConfirmed)
        {
            account.ConfirmEmail();
        }

        return account;
    }

    private static RefreshToken CreateRefreshToken(
        Guid accountId,
        string rawToken,
        bool isPersistent = false,
        DateTime? expiresAt = null,
        DateTime? loginAt = null,
        Guid? familyId = null)
    {
        return RefreshToken.Create(new RefreshTokenParams(
            accountId,
            TokenHash.Compute(rawToken),
            expiresAt ?? DateTime.UtcNow.AddDays(1),
            DeviceInfo: null,
            IpAddress: null,
            IsPersistent: isPersistent,
            LoginAt: loginAt ?? DateTime.UtcNow,
            FamilyId: familyId));
    }

    // LoginAsync

    [Fact]
    public async Task LoginAsync_UsernameNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        LoginRequest request = new("nva", "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_AccountInactive_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(status: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        LoginRequest request = new(account.Username, "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_PasswordHashNull_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(passwordHash: null);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        LoginRequest request = new(account.Username, "password123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_PasswordMismatch_ThrowsUnauthorized_AndCallsVerifyOnce()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        LoginRequest request = new(account.Username, "wrong-password");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.Received(1).Verify(request.Password, account.PasswordHash!);
    }

    // Temporary lockout

    [Fact]
    public async Task LoginAsync_Locked_RefusesWithTheLockMessage_WithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);
        LoginRequest request = new(account.Username, "correct-password");

        // Before the password check on purpose: refusing only a correct password would tell a guesser
        // which guess was right, and the lock would stop nothing.
        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AccountTemporarilyLocked,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
    }

    // Parallel guesses that reserved attempts past the limit while the last allowed one was still running.
    [Fact]
    public async Task LoginAsync_AttemptBeyondTheLimit_IsRefusedWithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(6);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AccountTemporarilyLocked,
            () => f.Service.LoginAsync(new LoginRequest(account.Username, "guess"), null, null, CancellationToken.None));

        f.PasswordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordOnTheLastAllowedAttempt_LocksTheAccount()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(5);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        DateTime before = DateTime.UtcNow;

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(new LoginRequest(account.Username, "wrong"), null, null, CancellationToken.None));

        await f.LoginAttempts.Received(1).LockAsync(
            PasswordAttemptPurpose.Login,
            account.Id,
            Arg.Is<DateTime>(until => until >= before.AddMinutes(15) && until <= DateTime.UtcNow.AddMinutes(15)),
            Arg.Any<CancellationToken>());
    }

    // A guesser who drops the connection during the hash check must not be able to skip the lock write.
    [Fact]
    public async Task LoginAsync_WrongPasswordOnTheLastAllowedAttempt_LocksEvenWhenTheRequestIsAborted()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(5);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        using CancellationTokenSource aborted = new();
        await aborted.CancelAsync();

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(new LoginRequest(account.Username, "wrong"), null, null, aborted.Token));

        await f.LoginAttempts.Received(1).LockAsync(
            PasswordAttemptPurpose.Login,
            account.Id, Arg.Any<DateTime>(), Arg.Is<CancellationToken>(token => !token.CanBeCanceled));
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordBelowTheLimit_DoesNotLock()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(4);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.LoginAsync(new LoginRequest(account.Username, "wrong"), null, null, CancellationToken.None));

        await f.LoginAttempts.DidNotReceiveWithAnyArgs().LockAsync(PasswordAttemptPurpose.Login, default, default, default);
        await f.LoginAttempts.DidNotReceiveWithAnyArgs().ClearAsync(PasswordAttemptPurpose.Login, default, default);
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ClearsTheCounter()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.LoginAttempts.ReserveAttemptAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(5);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await f.Service.LoginAsync(new LoginRequest(account.Username, "correct-password"), null, null, CancellationToken.None);

        await f.LoginAttempts.Received(1).ClearAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<CancellationToken>());
        await f.LoginAttempts.DidNotReceiveWithAnyArgs().LockAsync(PasswordAttemptPurpose.Login, default, default, default);
    }

    // The password was right; the account just isn't usable yet — keep counting and it would lock itself.
    [Fact]
    public async Task LoginAsync_CorrectPasswordButEmailNotConfirmed_StillClearsTheCounter()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.LoginAsync(new LoginRequest(account.Username, "correct-password"), null, null, CancellationToken.None));

        await f.LoginAttempts.Received(1).ClearAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<CancellationToken>());
    }

    // Unknown, deactivated and password-less accounts get the generic message and count nothing: there is no
    // password to guess, and a lock message would say more than "invalid" does.
    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive")]
    [InlineData("no-password")]
    public async Task LoginAsync_NothingToGuess_DoesNotCountTheAttempt(string state)
    {
        Fixture f = CreateFixture();
        Account? account = state switch
        {
            "inactive" => CreateAccount(status: false),
            "no-password" => CreateAccount(passwordHash: null),
            _ => null
        };
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.LoginAsync(new LoginRequest("nva", "guess"), null, null, CancellationToken.None));

        await f.LoginAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(PasswordAttemptPurpose.Login, default, default, default, default);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult_AndPersistsRefreshToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal("fake-access-token", result.AccessToken);
        Assert.Equal(account.Id, result.Account.Id);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        double diffSeconds = Math.Abs((result.AccessTokenExpiry - DateTime.UtcNow.AddMinutes(15)).TotalSeconds);
        Assert.True(diffSeconds < 5, $"Expected access token expiry within 5s tolerance, was off by {diffSeconds}s");
    }

    [Fact]
    public async Task LoginAsync_AccountWithAvatar_ReturnsThePublicAvatarUrl()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        account.SetAvatar("public/avatars/a.png");
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        AuthResult result = await f.Service.LoginAsync(new LoginRequest(account.Username, "correct-password"), null, null, CancellationToken.None);

        Assert.Equal("/storage/public/avatars/a.png", result.Account.AvatarUrl);
    }

    [Fact]
    public async Task LoginAsync_AccountWithoutAvatar_ReturnsNoAvatarUrl()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        AuthResult result = await f.Service.LoginAsync(new LoginRequest(account.Username, "correct-password"), null, null, CancellationToken.None);

        Assert.Null(result.Account.AvatarUrl);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginAsync_KeepLoggedIn_PassesIsPersistentToIssuedToken(bool keepLoggedIn)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password", keepLoggedIn);

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal(keepLoggedIn, result.IsPersistent);
    }

    // LoginAsync — email confirmation gate

    [Fact]
    public async Task LoginAsync_EmailNotConfirmed_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        LoginRequest request = new(account.Username, "correct-password");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailNotConfirmed,
            () => f.Service.LoginAsync(request, null, null, CancellationToken.None));

        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // ExternalLoginAsync

    [Fact]
    public async Task ExternalLoginAsync_UnsupportedProvider_ThrowsDomainException()
    {
        Fixture f = CreateFixture();
        ExternalLoginRequest request = new("some-credential");

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.ExternalLoginProviderNotSupported,
            () => f.Service.ExternalLoginAsync("github", request, null, null, CancellationToken.None));
    }

    private static ExternalUserInfo Info(string sub, string? email, bool verified)
        => new(sub, email, "Some User", EmailVerified: verified);

    private static void SeedExternalLogin(Fixture f, Account? linked, string provider = GoogleProviderName, string sub = "sub-1")
    {
        if (linked is null)
        {
            RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
            return;
        }

        ExternalLogin link = ExternalLogin.Create(new ExternalLoginParams(linked.Id, provider, sub, linked.Email));
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, [link]);
        f.AccountRepo.GetByIdAsync(linked.Id, Arg.Any<CancellationToken>()).Returns(linked);
    }

    [Fact]
    public async Task ExternalLoginAsync_LinkedBySub_UnverifiedToken_SignsIn()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        SeedExternalLogin(f, account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-1", account.Email, false));

        AuthResult result = (await f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None)).Auth;

        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task ExternalLoginAsync_LinkedAccountUnconfirmed_VerifiedTokenSameEmail_ConfirmsEmail()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(email: "nva@example.com", emailConfirmed: false);
        SeedExternalLogin(f, account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-1", "  NVA@Example.com ", true));

        await f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None);

        Assert.True(account.EmailConfirmed);
        f.AccountRepo.Received(1).Update(account);
    }

    [Theory]
    [InlineData("other@example.com", true)]
    [InlineData("nva@example.com", false)]
    public async Task ExternalLoginAsync_LinkedAccountUnconfirmed_DifferentEmailOrUnverified_StaysUnconfirmed(string tokenEmail, bool verified)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(email: "nva@example.com", emailConfirmed: false);
        SeedExternalLogin(f, account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-1", tokenEmail, verified));

        AuthResult result = (await f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None)).Auth;

        Assert.False(account.EmailConfirmed);
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatch_UnverifiedToken_ThrowsNotVerifiedByProviderAndNoLink()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "victim@example.com");
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-x", "victim@example.com", false));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatch_AccountUnconfirmed_ThrowsEmailNotConfirmedAndNoLink()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "pending@example.com", emailConfirmed: false);
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-x", "pending@example.com", true));

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.ExternalLoginEmailNotConfirmed,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatch_LockedAccount_Throws401AndNoLink()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "locked@example.com", status: false);
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-x", "locked@example.com", true));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidUsernameOrPassword,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_LinkedBySub_TokenWithoutEmail_SignsIn()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        SeedExternalLogin(f, account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-1", null, true));

        AuthResult result = (await f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None)).Auth;

        Assert.Equal("fake-access-token", result.AccessToken);
        Assert.False(account.EmailConfirmed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExternalLoginAsync_UnlinkedTokenWithoutEmail_ThrowsNotVerifiedByProviderAndCreatesNothing(string? tokenEmail)
    {
        Fixture f = CreateFixture();
        SeedExternalLogin(f, null);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-n", tokenEmail, true));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>());
        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_NewAccount_UnverifiedToken_ThrowsAndCreatesNothing()
    {
        Fixture f = CreateFixture();
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-new", "new@example.com", false));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // Review Focus 4: matching is on Account.Email only, never PendingEmail.
    [Fact]
    public async Task ExternalLoginAsync_TokenEmailEqualsSomeonesPendingEmail_DoesNotMatchThatAccount()
    {
        Fixture f = CreateFixture();
        Account other = CreateAccount(username: "other", email: "other@example.com");
        other.RequestEmailChange("target@example.com");
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [other]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-t", "target@example.com", true));

        await f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None);

        await f.ExternalLoginRepo.DidNotReceive().AddAsync(
            Arg.Is<ExternalLogin>(l => l != null && l.AccountId == other.Id), Arg.Any<CancellationToken>());
        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null && a.Email == "target@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_Google_KeepsEmailVerifiedRuleOnBothBranches()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "g@example.com");
        SeedExternalLogin(f, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("g-1", "g@example.com", false), Info("g-2", "fresh@example.com", false));

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));
        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.ExternalLoginEmailNotVerifiedByProvider,
            () => f.Service.ExternalLoginAsync("google", new ExternalLoginRequest("c"), null, null, CancellationToken.None));

        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_ExistingLink_LogsInWithoutCreatingAccountOrLink()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        ExternalLogin link = ExternalLogin.Create(new ExternalLoginParams(
            account.Id, GoogleProviderName, "google-sub-1", account.Email));
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, [link]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-1", account.Email, "Nguyen Van A", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = (await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None)).Auth;

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    // The lock defends against password guessing; signing in through the provider proves identity another
    // way, so it gets in and resets the counter.
    [Fact]
    public async Task ExternalLoginAsync_LockedAccount_SignsInAndClearsTheCounter()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        ExternalLogin link = ExternalLogin.Create(new ExternalLoginParams(
            account.Id, GoogleProviderName, "google-sub-1", account.Email));
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, [link]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-1", account.Email, "Nguyen Van A", EmailVerified: true));

        AuthResult result = (await f.Service.ExternalLoginAsync(
            "google", new ExternalLoginRequest("some-credential"), null, null, CancellationToken.None)).Auth;

        Assert.Equal("fake-access-token", result.AccessToken);
        await f.LoginAttempts.Received(1).ClearAsync(PasswordAttemptPurpose.Login, account.Id, Arg.Any<CancellationToken>());
        await f.LoginAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(PasswordAttemptPurpose.Login, default, default, default, default);
    }

    [Fact]
    public async Task ExternalLoginAsync_NoMatchingEmail_CreatesConfirmedAccountAndLinksProvider()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-2", "newuser@example.com", "New User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = (await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None)).Auth;

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null && a.Email == "newuser@example.com" && a.Username == "newuser" && a.EmailConfirmed),
            Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.Received(1).AddAsync(
            Arg.Is<ExternalLogin>(l => l != null && l.Provider == GoogleProviderName && l.ProviderUserId == "google-sub-2"),
            Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task ExternalLoginAsync_UsernameCollision_AppendsSuffix()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(
            f.AccountRepo, [CreateAccount(username: "newuser", email: "someone-else@example.com")]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-3", "newuser@example.com", "New User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None);

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null && a.Username == "newuser1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatchesUnconfirmedAccount_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "pending@example.com", emailConfirmed: false);
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-4", "pending@example.com", "Pending User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.ExternalLoginEmailNotConfirmed,
            () => f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.DidNotReceive().AddAsync(Arg.Any<ExternalLogin>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginAsync_EmailMatchesConfirmedAccount_AutoLinksWithoutCreatingNewAccount()
    {
        Fixture f = CreateFixture();
        Account existing = CreateAccount(email: "verified@example.com", emailConfirmed: true);
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [existing]);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-5", "verified@example.com", "Verified User", EmailVerified: true));
        ExternalLoginRequest request = new("some-credential");

        AuthResult result = (await f.Service.ExternalLoginAsync("google", request, null, null, CancellationToken.None)).Auth;

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.ExternalLoginRepo.Received(1).AddAsync(
            Arg.Is<ExternalLogin>(l => l != null && l.AccountId == existing.Id && l.ProviderUserId == "google-sub-5"),
            Arg.Any<CancellationToken>());
        Assert.Equal(existing.Id, result.Account.Id);
    }

    // AC-178: the invitation page accepts on its own only for an account this sign-in created.
    [Fact]
    public async Task ExternalLoginAsync_NewAccount_AccountCreatedTrue()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ExternalUserInfo("google-sub-6", "brandnew@example.com", "Brand New", EmailVerified: true));

        ExternalLoginResult result = await f.Service.ExternalLoginAsync(
            "google", new ExternalLoginRequest("c"), null, null, CancellationToken.None);

        Assert.True(result.AccountCreated);
        Assert.Equal("fake-access-token", result.Auth.AccessToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExternalLoginAsync_LinkedOrEmailMatched_AccountCreatedFalse(bool linked)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(email: "known@example.com", emailConfirmed: true);
        if (linked)
        {
            SeedExternalLogin(f, account);
        }
        else
        {
            RepositoryPredicateStub.StubFirstOrDefault(f.ExternalLoginRepo, []);
            RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account]);
        }

        f.ExternalAuthProvider.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Info("sub-1", account.Email, true));

        ExternalLoginResult result = await f.Service.ExternalLoginAsync(
            "google", new ExternalLoginRequest("c"), null, null, CancellationToken.None);

        Assert.False(result.AccountCreated);
        Assert.Equal(account.Id, result.Auth.Account.Id);
    }

    // RefreshTokenAsync

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshTokenAsync_BlankToken_ThrowsUnauthorized_ZeroRepoCalls(string? token)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.RefreshTokenRequired,
            () => f.Service.RefreshTokenAsync(token!, null, null, CancellationToken.None));

        await f.RefreshTokenRepo.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.RefreshTokenRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenRevoked_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        token.Revoke();
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_AccountNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(token.AccountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_AccountInactive_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(status: false);
        RefreshToken token = CreateRefreshToken(account.Id, "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("some-token", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RevokesOldToken_AndIssuesNewOne()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        RefreshToken oldToken = CreateRefreshToken(account.Id, "old-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.NotNull(oldToken.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(oldToken);
        Assert.NotEqual("old-token", result.RefreshToken);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshTokenAsync_ValidToken_CarriesOldIsPersistentForward(bool oldIsPersistent)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        RefreshToken oldToken = CreateRefreshToken(account.Id, "old-token", isPersistent: oldIsPersistent);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.Equal(oldIsPersistent, result.IsPersistent);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_KeepsFamilyIdOnNewToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid familyId = Guid.NewGuid();
        RefreshToken oldToken = CreateRefreshToken(account.Id, "old-token", familyId: familyId);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        RefreshToken? issued = null;
        f.RefreshTokenRepo.When(r => r.AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>()))
            .Do(ci => issued = ci.Arg<RefreshToken>());

        await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.NotNull(issued);
        Assert.Equal(familyId, issued!.FamilyId);
    }

    [Fact]
    public async Task RefreshTokenAsync_StaleTokenWithinGrace_RotatesActiveSuccessor()
    {
        Fixture f = CreateFixture(refreshTokenReuseGraceSeconds: 60);
        Account account = CreateAccount();
        Guid familyId = Guid.NewGuid();
        RefreshToken staleToken = CreateRefreshToken(account.Id, "stale-token", familyId: familyId);
        staleToken.Revoke();
        RefreshToken successor = CreateRefreshToken(account.Id, "successor-token", familyId: familyId);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [staleToken, successor]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.RefreshTokenAsync("stale-token", null, null, CancellationToken.None);

        // The concurrent-tab refresh is honoured: the still-active successor is the
        // one that gets rotated, and a fresh pair is issued in the same family.
        Assert.NotNull(successor.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(successor);
        Assert.NotEqual("stale-token", result.RefreshToken);
        Assert.NotEqual("successor-token", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_StaleTokenOutsideGrace_BurnsWholeFamily()
    {
        Fixture f = CreateFixture(refreshTokenReuseGraceSeconds: 0);
        Account account = CreateAccount();
        Guid familyId = Guid.NewGuid();
        RefreshToken staleToken = CreateRefreshToken(account.Id, "stale-token", familyId: familyId);
        staleToken.Revoke();
        // Two live successors — a truly-simultaneous double refresh is a normal state.
        RefreshToken successorA = CreateRefreshToken(account.Id, "successor-a", familyId: familyId);
        RefreshToken successorB = CreateRefreshToken(account.Id, "successor-b", familyId: familyId);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [staleToken, successorA, successorB]);
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [staleToken, successorA, successorB]);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("stale-token", null, null, CancellationToken.None));

        // Replay of a long-dead token while the family is still live is treated as a
        // leak: every active token in the family is revoked.
        Assert.NotNull(successorA.RevokedAt);
        Assert.NotNull(successorB.RevokedAt);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // RevokeTokenAsync

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_BlankToken_ReturnsSilently_NoRepoCalls(string? token)
    {
        Fixture f = CreateFixture();

        await f.Service.RevokeTokenAsync(token!, CancellationToken.None);

        await f.RefreshTokenRepo.DidNotReceive().FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeTokenAsync_TokenNotFound_ReturnsSilently_NoSaveChanges()
    {
        Fixture f = CreateFixture();
        f.RefreshTokenRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        await f.Service.RevokeTokenAsync("some-token", CancellationToken.None);

        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeTokenAsync_TokenFound_RevokesAndSaves()
    {
        Fixture f = CreateFixture();
        RefreshToken token = CreateRefreshToken(Guid.NewGuid(), "some-token");
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [token]);

        await f.Service.RevokeTokenAsync("some-token", CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(token);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Organization scoping

    [Fact]
    public async Task LoginAsync_AccountBelongsToExactlyOneOrganization_IssuesTokenScopedToThatOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        OrganizationMember membership = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, account.Id));
        f.OrganizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([membership]);
        RepositoryPredicateStub.StubListAsync(f.OrganizationRepo, [organization]);
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(organization.Name, result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organization.Id, Arg.Any<Guid>());
    }

    [Fact]
    public async Task LoginAsync_AccountBelongsToMultipleOrganizations_IssuesTokenWithNoOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Organization first = Organization.Create(new OrganizationParams("First", "first"));
        Organization second = Organization.Create(new OrganizationParams("Second", "second"));
        f.OrganizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([
                OrganizationMember.Create(new OrganizationMemberParams(first.Id, account.Id)),
                OrganizationMember.Create(new OrganizationMemberParams(second.Id, account.Id))
            ]);
        RepositoryPredicateStub.StubListAsync(f.OrganizationRepo, [first, second]);
        LoginRequest request = new(account.Username, "correct-password");

        AuthResult result = await f.Service.LoginAsync(request, null, null, CancellationToken.None);

        Assert.Null(result.OrganizationId);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, null, Arg.Any<Guid>());
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenHasOrganization_PreservesOrganizationOnNewToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();
        RefreshToken oldToken = RefreshToken.Create(new RefreshTokenParams(
            account.Id, TokenHash.Compute("old-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, organizationId));
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(Organization.Create(new OrganizationParams("Acme", "acme")));

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.Equal(organizationId, result.OrganizationId);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organizationId, Arg.Any<Guid>());
    }

    private static RefreshToken CreateOrganizationRefreshToken(
        Guid accountId, Guid organizationId, Guid familyId, bool isPersistent, DateTime loginAt)
    {
        return RefreshToken.Create(new RefreshTokenParams(
            accountId,
            TokenHash.Compute("old-token"),
            DateTime.UtcNow.AddDays(1),
            null,
            null,
            isPersistent,
            loginAt,
            organizationId,
            familyId));
    }

    // QĐ-31: a removed member or a suspended organization (AC-183, AC-199) ends only the organization context —
    // the session keeps its family, persistence and sign-in time, so the client can say why the organization is gone.
    [Theory]
    [InlineData(TenantAccessStatus.NotMember, true)]
    [InlineData(TenantAccessStatus.Suspended, false)]
    public async Task RefreshTokenAsync_RemovedMemberOrSuspendedOrganization_IssuesTokensWithoutOrganizationSameFamily(
        TenantAccessStatus access, bool isPersistent)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();
        Guid familyId = Guid.NewGuid();
        DateTime loginAt = DateTime.UtcNow.AddHours(-3);
        RefreshToken oldToken = CreateOrganizationRefreshToken(
            account.Id, organizationId, familyId, isPersistent, loginAt);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.TenantAccessService.GetAccessAsync(account.Id, organizationId, Arg.Any<CancellationToken>())
            .Returns(access);
        RefreshToken? issued = null;
        f.RefreshTokenRepo.When(r => r.AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>()))
            .Do(ci => issued = ci.Arg<RefreshToken>());

        AuthResult result = await f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.OrganizationName);
        Assert.Empty(result.Permissions);
        Assert.Equal(isPersistent, result.IsPersistent);
        Assert.NotNull(oldToken.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(oldToken);
        Assert.NotNull(issued);
        Assert.Equal(familyId, issued!.FamilyId);
        Assert.Null(issued.OrganizationId);
        Assert.Equal(isPersistent, issued.IsPersistent);
        Assert.Equal(loginAt, issued.LoginAt);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, null, familyId);
        await f.OrganizationRepo.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task RefreshTokenAsync_LockedAccount_StillThrows401()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(status: false);
        Guid organizationId = Guid.NewGuid();
        RefreshToken oldToken = CreateOrganizationRefreshToken(
            account.Id, organizationId, Guid.NewGuid(), false, DateTime.UtcNow);
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [oldToken]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.TenantAccessService.GetAccessAsync(account.Id, organizationId, Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.NotMember);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None));

        Assert.Null(oldToken.RevokedAt);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await f.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        f.JwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default, default);
        await f.TenantAccessService.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default, default);
    }

    // Reuse detection runs before the organization is looked at: losing the organization never rescues a replay.
    [Fact]
    public async Task RefreshTokenAsync_StaleTokenOfSuspendedOrganization_BurnsFamily()
    {
        Fixture f = CreateFixture(refreshTokenReuseGraceSeconds: 0);
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();
        Guid familyId = Guid.NewGuid();
        RefreshToken staleToken = CreateOrganizationRefreshToken(
            account.Id, organizationId, familyId, false, DateTime.UtcNow);
        staleToken.Revoke();
        RefreshToken successor = RefreshToken.Create(new RefreshTokenParams(
            account.Id, TokenHash.Compute("successor-token"), DateTime.UtcNow.AddDays(1), null, null, false,
            DateTime.UtcNow, organizationId, familyId));
        RepositoryPredicateStub.StubFirstOrDefault(f.RefreshTokenRepo, [staleToken, successor]);
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [staleToken, successor]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.TenantAccessService.GetAccessAsync(account.Id, organizationId, Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.Suspended);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidRefreshToken,
            () => f.Service.RefreshTokenAsync("old-token", null, null, CancellationToken.None));

        Assert.NotNull(successor.RevokedAt);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        f.JwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default, default);
        await f.TenantAccessService.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default, default);
    }

    // SwitchOrganizationAsync

    // AC-320: a non-member learns nothing about the organization, not even that it is suspended.
    [Fact]
    public async Task SwitchOrganizationAsync_NotMemberOfSuspendedOrganization_ThrowsAccessDeniedWithoutName()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        f.TenantAccessService.GetAccessAsync(accountId, organization.Id, Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.NotMember);

        ForbiddenException exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.SwitchOrganizationAsync(organization.Id, null, null, CancellationToken.None));

        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, exception.Message);
        Assert.Empty(exception.Extensions);
        await f.OrganizationRepo.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // AC-301 (API-28): a member of a suspended organization is told which one.
    [Fact]
    public async Task SwitchOrganizationAsync_SuspendedOrganizationMember_ThrowsSuspendedWithName()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        f.TenantAccessService.GetAccessAsync(accountId, organization.Id, Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.Suspended);

        ForbiddenException exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.SwitchOrganizationAsync(organization.Id, null, null, CancellationToken.None));

        Assert.Equal(ApplicationMessages.OrganizationSuspended, exception.Message);
        Assert.Equal(organization.Name, exception.Extensions[ProblemExtensionKeys.OrganizationName]);
        Assert.Single(exception.Extensions);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        f.JwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default, default);
    }

    // The organization row vanished after the status was read: no name to give, so the plain denial.
    [Fact]
    public async Task SwitchOrganizationAsync_SuspendedOrganizationGone_ThrowsAccessDeniedWithoutName()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        Guid organizationId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns((Organization?)null);
        f.TenantAccessService.GetAccessAsync(accountId, organizationId, Arg.Any<CancellationToken>())
            .Returns(TenantAccessStatus.Suspended);

        ForbiddenException exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.SwitchOrganizationAsync(organizationId, null, null, CancellationToken.None));

        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, exception.Message);
        Assert.Empty(exception.Extensions);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task SwitchOrganizationAsync_ActiveMember_IssuesTokenScopedToOrganization()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        AuthResult result = await f.Service.SwitchOrganizationAsync(
            organization.Id, null, null, CancellationToken.None);

        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(organization.Name, result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organization.Id, Arg.Any<Guid>());
        await f.TenantAccessService.Received(1)
            .GetAccessAsync(account.Id, organization.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchOrganizationAsync_NullOrganizationId_IssuesTokenWithNoOrganizationWithoutMembershipCheck()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None);

        Assert.Null(result.OrganizationId);
        Assert.Null(result.OrganizationName);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, null, Arg.Any<Guid>());
        await f.TenantAccessService.DidNotReceiveWithAnyArgs().GetAccessAsync(default, default, default);
    }

    [Fact]
    public async Task SwitchOrganizationAsync_InASession_KeepsTheSessionAndRetiresItsPreviousToken()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid sessionId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(sessionId);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        RefreshToken previous = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "previous-hash", DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, FamilyId: sessionId));
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [previous]);

        await f.Service.SwitchOrganizationAsync(organization.Id, null, null, CancellationToken.None);

        Assert.NotNull(previous.RevokedAt);
        f.RefreshTokenRepo.Received().Update(previous);
        f.JwtTokenService.Received(1).GenerateAccessToken(account, organization.Id, sessionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchOrganizationAsync_InASession_KeepsTheSessionsRememberMeChoice(bool isPersistent)
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid sessionId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(sessionId);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        f.OrganizationRepo.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        RefreshToken previous = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "previous-hash", DateTime.UtcNow.AddDays(1), null, null, isPersistent, DateTime.UtcNow, FamilyId: sessionId));
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [previous]);

        AuthResult result = await f.Service.SwitchOrganizationAsync(organization.Id, null, null, CancellationToken.None);

        Assert.Equal(isPersistent, result.IsPersistent);
        // Later refreshes copy the stored flag, so the saved row must carry it too.
        await f.RefreshTokenRepo.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.IsPersistent == isPersistent), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchOrganizationAsync_InASession_KeepsTheEarliestSignInTime()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid sessionId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(sessionId);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        DateTime signedInAt = DateTime.UtcNow.AddDays(-3);
        RefreshToken earliest = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "earliest-hash", DateTime.UtcNow.AddDays(1), null, null, false, signedInAt, FamilyId: sessionId));
        RefreshToken later = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "later-hash", DateTime.UtcNow.AddDays(1), null, null, false, signedInAt.AddDays(1), FamilyId: sessionId));
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [later, earliest]);

        await f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None);

        await f.RefreshTokenRepo.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.LoginAt == signedInAt), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchOrganizationAsync_NoSessionId_IssuesANonPersistentSession()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        AuthResult result = await f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None);

        Assert.False(result.IsPersistent);
    }

    [Fact]
    public async Task SwitchOrganizationAsync_SessionWithNoActiveToken_ThrowsUnauthorizedAndIssuesNothing()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(Guid.NewGuid());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, []);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None));
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        f.RefreshTokenRepo.DidNotReceive().Update(Arg.Any<RefreshToken>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchOrganizationAsync_SessionWhoseOnlyOpenTokenExpired_ThrowsUnauthorizedAndIssuesNothing()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid sessionId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(sessionId);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RefreshToken expired = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "expired-hash", DateTime.UtcNow.AddDays(1), null, null, true, DateTime.UtcNow.AddDays(-8), FamilyId: sessionId));
        ForceExpired(expired);
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [expired]);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None));
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchOrganizationAsync_ExpiredSiblingToken_DoesNotLeakItsChoiceOrSignInTime()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Guid sessionId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.CurrentUserService.SessionId.Returns(sessionId);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        DateTime activeSignIn = DateTime.UtcNow.AddDays(-1);
        RefreshToken expired = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "expired-hash", DateTime.UtcNow.AddDays(1), null, null, true, DateTime.UtcNow.AddDays(-9), FamilyId: sessionId));
        ForceExpired(expired);
        RefreshToken active = RefreshToken.Create(new RefreshTokenParams(
            account.Id, "active-hash", DateTime.UtcNow.AddDays(1), null, null, false, activeSignIn, FamilyId: sessionId));
        RepositoryPredicateStub.StubListAsync(f.RefreshTokenRepo, [expired, active]);

        await f.Service.SwitchOrganizationAsync(null, null, null, CancellationToken.None);

        await f.RefreshTokenRepo.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => !t.IsPersistent && t.LoginAt == activeSignIn), Arg.Any<CancellationToken>());
    }

    // Create refuses a past expiry, so a token that expired after it was issued is made by setting the property.
    private static void ForceExpired(RefreshToken token) =>
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.ExpiresAt))!.SetValue(token, DateTime.UtcNow.AddMinutes(-1));
}
