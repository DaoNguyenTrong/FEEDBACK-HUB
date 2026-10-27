using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
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

public class RegistrationServiceTests
{
    private sealed record Fixture(
        RegistrationService Service,
        IRepository<Account, Guid> AccountRepo,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IRepository<EmailVerificationToken, Guid> EmailVerificationTokenRepo,
        IUnitOfWork UnitOfWork,
        IPasswordHasher PasswordHasher,
        IEmailSender EmailSender,
        IDateTimeProvider Clock);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        IRepository<EmailVerificationToken, Guid> emailVerificationTokenRepo =
            Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        IRepository<OrganizationMember, Guid> organizationMemberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        unitOfWork.Repository<EmailVerificationToken, Guid>().Returns(emailVerificationTokenRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(organizationMemberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        organizationMemberRepo.ListAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IJwtTokenService jwtTokenService = Substitute.For<IJwtTokenService>();
        jwtTokenService.GenerateAccessToken(Arg.Any<Account>(), Arg.Any<Guid?>(), Arg.Any<Guid>()).Returns("fake-access-token");
        jwtTokenService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString());

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();
        permissionResolver.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        IEmailSender emailSender = Substitute.For<IEmailSender>();

        IOptions<JwtSettings> jwtOptions = Options.Create(new JwtSettings
        {
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        });

        IOptions<EmailSettings> emailOptions = Options.Create(new EmailSettings
        {
            FrontendBaseUrl = "https://app.example.com",
            VerificationTokenExpiryHours = 24
        });

        // Same rationale as AuthServiceTests: TokenIssuer has no I/O beyond the collaborators already
        // substituted here, so wiring the real implementation preserves VerifyEmailAsync's pre-split
        // token-issuing assertions unchanged.
        ITokenIssuer tokenIssuer = new TokenIssuer(
            unitOfWork,
            jwtTokenService,
            permissionResolver,
            Substitute.For<ISystemAdministratorResolver>(),
            jwtOptions,
            Options.Create(new StorageSettings()));

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);
        EmailChangeLink emailChangeLink = new(
            unitOfWork, emailSender, emailOptions, clock, NullLogger<EmailChangeLink>.Instance);

        RegistrationService service = new(
            unitOfWork,
            passwordHasher,
            emailSender,
            emailOptions,
            tokenIssuer,
            emailChangeLink,
            clock,
            NullLogger<RegistrationService>.Instance);

        return new Fixture(
            service,
            accountRepo,
            refreshTokenRepo,
            emailVerificationTokenRepo,
            unitOfWork,
            passwordHasher,
            emailSender,
            clock);
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

    private static EmailVerificationToken CreateEmailVerificationToken(
        Guid accountId,
        string tokenHash = "hashed-token",
        DateTime? expiresAt = null)
    {
        return EmailVerificationToken.Create(new EmailVerificationTokenParams(
            accountId,
            tokenHash,
            expiresAt ?? DateTime.UtcNow.AddHours(1),
            EmailVerificationPurpose.VerifyEmail,
            "nva@example.com"));
    }

    private static EmailVerificationToken CreateToken(
        Guid accountId,
        string rawToken,
        EmailVerificationPurpose purpose,
        string email) =>
        EmailVerificationToken.Create(new EmailVerificationTokenParams(
            accountId, TokenHash.Compute(rawToken), DateTime.UtcNow.AddHours(1), purpose, email));

    private static RegisterRequest CreateRegisterRequest(
        string username = "nva",
        string email = "nva@example.com")
    {
        return new RegisterRequest(
            Name: "Nguyen Van A",
            Username: username,
            Email: email,
            Password: "password123");
    }

    // RegisterAsync

    // The first argument keeps each case's former method name in the test report.
    [Theory]
    [InlineData("RegisterAsync_UsernameExists_ThrowsConflict",
        "nva", "nva@example.com", "nva", "nva@example.com", ApplicationMessages.AccountUsernameAlreadyExists)]
    [InlineData("RegisterAsync_EmailExists_ThrowsConflict",
        "other", "nva@example.com", "nva", "nva@example.com", ApplicationMessages.AccountEmailAlreadyExists)]
    [InlineData("RegisterAsync_EmailDiffersOnlyByCase_ThrowsConflict",
        "other", "nva@example.com", "nva", " NVA@Example.com ", ApplicationMessages.AccountEmailAlreadyExists)]
    public async Task RegisterAsync_Duplicate_ThrowsConflict(
        string caseName,
        string existingUsername,
        string existingEmail,
        string requestedUsername,
        string requestedEmail,
        string expectedMessage)
    {
        _ = caseName;
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(
            f.AccountRepo,
            [CreateAccount(username: existingUsername, email: existingEmail)]);
        RegisterRequest request = CreateRegisterRequest(username: requestedUsername, email: requestedEmail);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            expectedMessage,
            () => f.Service.RegisterAsync(request, CancellationToken.None));

        await f.AccountRepo.DidNotReceive().AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_UniqueCredentials_CreatesUnconfirmedAccountAndSendsVerificationEmail()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.PasswordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        RegisterRequest request = CreateRegisterRequest();

        RegisterResult result = await f.Service.RegisterAsync(request, CancellationToken.None);

        await f.AccountRepo.Received(1).AddAsync(
            Arg.Is<Account>(a => a != null
                && a.Username == request.Username
                && a.Email == request.Email
                && a.Status
                && !a.EmailConfirmed),
            Arg.Any<CancellationToken>());
        f.PasswordHasher.Received(1).Hash(request.Password);
        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.EmailSender.Received(1).SendAsync(
            request.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        Assert.Equal(request.Email, result.Email);
    }

    [Fact]
    public async Task RegisterAsync_EmailSendFails_StillCommitsAccountAndDoesNotPropagate()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, []);
        f.PasswordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        f.EmailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        RegisterRequest request = CreateRegisterRequest();

        RegisterResult result = await f.Service.RegisterAsync(request, CancellationToken.None);

        Assert.Equal(request.Email, result.Email);
        await f.AccountRepo.Received(1).AddAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>());
        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // VerifyEmailAsync

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConfirmsAccountAndIssuesTokens()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "raw-verification-token";
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, TokenHash.Compute(rawToken));
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        VerifyEmailRequest request = new(rawToken);

        AuthResult result = await f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None);

        Assert.True(account.EmailConfirmed);
        Assert.NotNull(token.ConsumedAt);
        f.AccountRepo.Received(1).Update(account);
        f.EmailVerificationTokenRepo.Received(1).Update(token);
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        Assert.Equal("fake-access-token", result.AccessToken);
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenNotFound_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, []);
        VerifyEmailRequest request = new("unknown-token");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "expired-token";
        EmailVerificationToken token = CreateEmailVerificationToken(
            account.Id, TokenHash.Compute(rawToken), expiresAt: DateTime.UtcNow.AddMinutes(1));
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);

        // Force expiry without violating the domain's "must expire in the future" invariant at creation time.
        typeof(EmailVerificationToken)
            .GetProperty(nameof(EmailVerificationToken.ExpiresAt))!
            .SetValue(token, DateTime.UtcNow.AddMinutes(-1));

        VerifyEmailRequest request = new(rawToken);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyConsumedToken_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        const string rawToken = "consumed-token";
        EmailVerificationToken token = CreateEmailVerificationToken(account.Id, TokenHash.Compute(rawToken));
        token.Consume();
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        VerifyEmailRequest request = new(rawToken);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_ChangeEmailToken_Throws401NoSession()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        EmailVerificationToken token = CreateToken(
            account.Id, "change-token", EmailVerificationPurpose.ChangeEmail, account.Email);
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(new VerifyEmailRequest("change-token"), null, null, CancellationToken.None));

        Assert.False(account.EmailConfirmed);
        await f.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenEmailDiffersFromAccountEmail_Throws401()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        EmailVerificationToken token = CreateToken(
            account.Id, "other-address-token", EmailVerificationPurpose.VerifyEmail, "someone-else@example.com");
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(new VerifyEmailRequest("other-address-token"), null, null, CancellationToken.None));

        Assert.False(account.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmailAsync_LegacyTokenWithoutEmail_Throws401()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        EmailVerificationToken token = CreateToken(
            account.Id, "legacy-token", EmailVerificationPurpose.VerifyEmail, account.Email);
        // Rows from before the column existed have neither a purpose nor an address.
        typeof(EmailVerificationToken).GetProperty(nameof(EmailVerificationToken.Email))!.SetValue(token, null);
        typeof(EmailVerificationToken).GetProperty(nameof(EmailVerificationToken.Purpose))!.SetValue(token, null);
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, [token]);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailVerificationTokenInvalidOrExpired,
            () => f.Service.VerifyEmailAsync(new VerifyEmailRequest("legacy-token"), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AccountWithoutPassword_SendsNothing()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false, passwordHash: null);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);

        await f.Service.ResendVerificationEmailAsync(new ResendVerificationRequest(account.Email), CancellationToken.None);

        await f.EmailVerificationTokenRepo.DidNotReceive().AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_NotYetConfirmed_StoresTokenBoundToPurposeAndAddress()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);

        await f.Service.ResendVerificationEmailAsync(new ResendVerificationRequest(account.Email), CancellationToken.None);

        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Is<EmailVerificationToken>(t =>
                t != null && t.Purpose == EmailVerificationPurpose.VerifyEmail && t.Email == account.Email),
            Arg.Any<CancellationToken>());
    }

    // ConfirmEmailChangeAsync

    private sealed record ChangeSetup(
        Fixture F, Account Account, EmailVerificationToken Token, List<EmailVerificationToken> Tokens);

    private static ChangeSetup CreateChangeSetup(string rawToken = "change-link")
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        account.RequestEmailChange("new@example.com");
        EmailVerificationToken token = CreateToken(
            account.Id, rawToken, EmailVerificationPurpose.ChangeEmail, "new@example.com");
        EmailVerificationToken verifyToken = CreateToken(
            account.Id, "other-verify", EmailVerificationPurpose.VerifyEmail, account.Email);
        List<EmailVerificationToken> tokens = [token, verifyToken];
        RepositoryPredicateStub.StubFirstOrDefault(f.EmailVerificationTokenRepo, tokens);
        RepositoryPredicateStub.StubListAsync(f.EmailVerificationTokenRepo, tokens);
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        return new ChangeSetup(f, account, token, tokens);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_ValidLink_AppliesChangeAndConsumesAllTokens()
    {
        ChangeSetup s = CreateChangeSetup();

        await s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None);

        Assert.Equal("new@example.com", s.Account.Email);
        Assert.True(s.Account.EmailConfirmed);
        Assert.Null(s.Account.PendingEmail);
        Assert.All(s.Tokens, t => Assert.NotNull(t.ConsumedAt));
        await s.F.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await s.F.RefreshTokenRepo.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_UnknownUsedExpiredOrVerifyEmailToken_Throws409NoLongerValid()
    {
        // Unknown
        ChangeSetup unknown = CreateChangeSetup();
        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => unknown.F.Service.ConfirmEmailChangeAsync(
                new ConfirmEmailChangeRequest("no-such-link"), CancellationToken.None));

        // Already used
        ChangeSetup used = CreateChangeSetup();
        used.Token.Consume();
        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => used.F.Service.ConfirmEmailChangeAsync(
                new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        // Expired: the clock moves a minute past ExpiresAt
        ChangeSetup expired = CreateChangeSetup();
        expired.F.Clock.UtcNow.Returns(expired.Token.ExpiresAt.AddMinutes(1));
        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => expired.F.Service.ConfirmEmailChangeAsync(
                new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        // A VerifyEmail token
        ChangeSetup wrongPurpose = CreateChangeSetup();
        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => wrongPurpose.F.Service.ConfirmEmailChangeAsync(
                new ConfirmEmailChangeRequest("other-verify"), CancellationToken.None));

        foreach (ChangeSetup setup in new[] { unknown, used, expired, wrongPurpose })
        {
            Assert.Equal("nva@example.com", setup.Account.Email);
            await setup.F.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_PendingChangedSinceLink_Throws409NoLongerValid()
    {
        ChangeSetup s = CreateChangeSetup();
        s.Account.RequestEmailChange("newer@example.com");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        Assert.Equal("nva@example.com", s.Account.Email);
        Assert.Equal("newer@example.com", s.Account.PendingEmail);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_TokenWithoutEmail_Throws409NoLongerValid()
    {
        ChangeSetup s = CreateChangeSetup();
        typeof(EmailVerificationToken).GetProperty(nameof(EmailVerificationToken.Email))!.SetValue(s.Token, null);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        Assert.Equal("nva@example.com", s.Account.Email);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_AddressTakenMeanwhile_Throws409AccountEmailAlreadyExists_OtherAccountUntouched()
    {
        ChangeSetup s = CreateChangeSetup();
        Account other = CreateAccount(username: "other", email: "new@example.com");
        s.F.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(other);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists,
            () => s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        Assert.Equal("nva@example.com", s.Account.Email);
        Assert.Equal("new@example.com", s.Account.PendingEmail);
        Assert.Null(s.Token.ConsumedAt);
        s.F.AccountRepo.DidNotReceive().Update(Arg.Any<Account>());
        await s.F.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_SecondOpenOfSameLink_Throws409()
    {
        ChangeSetup s = CreateChangeSetup();
        await s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailChangeLinkNoLongerValid,
            () => s.F.Service.ConfirmEmailChangeAsync(new ConfirmEmailChangeRequest("change-link"), CancellationToken.None));

        Assert.Equal("new@example.com", s.Account.Email);
    }

    // ResendVerificationEmailAsync

    [Fact]
    public async Task ResendVerificationEmailAsync_AccountNotFound_ReturnsSilently()
    {
        Fixture f = CreateFixture();
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        ResendVerificationRequest request = new("missing@example.com");

        await f.Service.ResendVerificationEmailAsync(request, CancellationToken.None);

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AlreadyConfirmed_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: true);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        ResendVerificationRequest request = new(account.Email);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.EmailAlreadyConfirmed,
            () => f.Service.ResendVerificationEmailAsync(request, CancellationToken.None));

        await f.EmailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_NotYetConfirmed_SendsNewVerificationEmail()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        ResendVerificationRequest request = new(account.Email);

        await f.Service.ResendVerificationEmailAsync(request, CancellationToken.None);

        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await f.EmailSender.Received(1).SendAsync(
            account.Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_EmailSendFails_DoesNotPropagate()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(emailConfirmed: false);
        f.AccountRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Account, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(account);
        f.EmailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        ResendVerificationRequest request = new(account.Email);

        await f.Service.ResendVerificationEmailAsync(request, CancellationToken.None);

        await f.EmailVerificationTokenRepo.Received(1).AddAsync(
            Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
    }
}
