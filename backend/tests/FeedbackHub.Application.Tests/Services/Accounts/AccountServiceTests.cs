using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Accounts;

public class AccountServiceTests
{
    private sealed record Fixture(
        AccountService Service,
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService,
        IPasswordHasher PasswordHasher,
        IPasswordAttemptStore PasswordAttempts,
        IEmailSender EmailSender,
        List<EmailVerificationToken> Tokens,
        ILogger<EmailChangeLink> LinkLogger);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        // First attempt of a fresh window unless a test says otherwise.
        IPasswordAttemptStore passwordAttempts = Substitute.For<IPasswordAttemptStore>();
        passwordAttempts.ReserveAttemptAsync(
                Arg.Any<PasswordAttemptPurpose>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(1);

        IEmailSender emailSender = Substitute.For<IEmailSender>();
        List<EmailVerificationToken> tokens = [];
        IRepository<EmailVerificationToken, Guid> tokenRepo = Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        unitOfWork.Repository<EmailVerificationToken, Guid>().Returns(tokenRepo);
        RepositoryPredicateStub.StubListAsync(tokenRepo, tokens);
        tokenRepo.AddAsync(Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call[0] is EmailVerificationToken added)
                {
                    tokens.Add(added);
                }

                return Task.CompletedTask;
            });
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);
        ILogger<EmailChangeLink> linkLogger = Substitute.For<ILogger<EmailChangeLink>>();
        EmailChangeLink link = new(
            unitOfWork,
            emailSender,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.example.com" }),
            clock,
            linkLogger);

        AccountService service = new(
            unitOfWork,
            currentUserService,
            passwordHasher,
            passwordAttempts,
            Options.Create(new LoginLockoutSettings()),
            Substitute.For<IStorageService>(),
            Options.Create(new StorageSettings()),
            link,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

        return new Fixture(
            service, accountRepo, unitOfWork, currentUserService, passwordHasher, passwordAttempts, emailSender, tokens, linkLogger);
    }

    private static Account CreateAccount(
        string name = "Nguyen Van A",
        string username = "nva",
        string email = "nva@example.com",
        string? passwordHash = "hashed-password")
    {
        Account account = Account.Create(new AccountParams(name, username, email));

        if (passwordHash is not null)
        {
            account.SetPasswordHash(passwordHash);
        }

        return account;
    }

    // GetCurrentProfileAsync

    [Fact]
    public async Task GetCurrentProfileAsync_UnparsableUserId_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetCurrentProfileAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentProfileAsync_AccountMissing_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(id.ToString());
        f.AccountRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Account?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetCurrentProfileAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentProfileAsync_Valid_ReturnsProfileDto()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        ProfileDto dto = await f.Service.GetCurrentProfileAsync(CancellationToken.None);

        Assert.Equal(account.Id, dto.Id);
        Assert.True(dto.HasPassword);
    }

    // UpdateCurrentProfileAsync

    [Fact]
    public async Task UpdateCurrentProfileAsync_EmailCollisionWithOtherAccount_ThrowsConflict()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        Account other = CreateAccount(username: "other", email: "taken@example.com");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account, other]);
        f.PasswordHasher.Verify("pw", Arg.Any<string>()).Returns(true);
        UpdateProfileRequest request = new("Name", null, null, null, "taken@example.com", "pw");

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists,
            () => f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None));

        f.AccountRepo.DidNotReceive().Update(Arg.Any<Account>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_SameEmailAsOwnAccount_DoesNotThrow()
    {
        // Guards the predicate's self-exclusion clause (candidate.Id != account.Id) — without it,
        // a user keeping their own unchanged email would incorrectly collide with themselves.
        Fixture f = CreateFixture();
        Account account = CreateAccount(email: "nva@example.com");
        Account other = CreateAccount(username: "other", email: "taken@example.com");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account, other]);
        UpdateProfileRequest request = new("New Name", null, null, null, "nva@example.com");

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None);

        Assert.Equal("New Name", dto.Name);
        Assert.Equal("nva@example.com", dto.Email);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_Success_PreservesUsernameAndStatus()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(username: "keep-username");
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account]);
        UpdateProfileRequest request = new("New Name", "0123456789", "Dev", "Address", "nva@example.com");

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(request, CancellationToken.None);

        Assert.Equal("keep-username", dto.Username);
        Assert.Equal("New Name", dto.Name);
        Assert.Equal("nva@example.com", dto.Email);
        Assert.True(account.Status);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // UpdateCurrentProfileAsync: email change (API-29, BR-82)

    private static Fixture EmailFixture(
        out Account account,
        string? passwordHash = "hashed-password",
        string email = "a@x.com",
        string? pending = null,
        params Account[] others)
    {
        Fixture f = CreateFixture();
        account = CreateAccount(email: email, passwordHash: passwordHash);
        if (pending is not null)
        {
            account.RequestEmailChange(pending);
        }

        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        RepositoryPredicateStub.StubFirstOrDefault(f.AccountRepo, [account, .. others]);
        f.PasswordHasher.Verify("pw", Arg.Any<string>()).Returns(true);
        return f;
    }

    private static UpdateProfileRequest Req(string email, string? password, string name = "Name") =>
        new(name, null, null, null, email, password);

    // ResendEmailChangeAsync / CancelEmailChangeAsync (API-30, API-31)

    [Fact]
    public async Task ResendEmailChangeAsync_Pending_ConsumesOldIssuesNew_NoPasswordAsked()
    {
        Fixture f = EmailFixture(out Account account, pending: "b@x.com");
        EmailVerificationToken old = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id, "old-hash", DateTime.UtcNow.AddHours(1), EmailVerificationPurpose.ChangeEmail, "b@x.com"));
        f.Tokens.Add(old);

        await f.Service.ResendEmailChangeAsync(CancellationToken.None);

        Assert.NotNull(old.ConsumedAt);
        Assert.Equal(2, f.Tokens.Count);
        Assert.Null(f.Tokens[1].ConsumedAt);
        Assert.Equal("b@x.com", f.Tokens[1].Email);
        await f.EmailSender.Received(1).SendAsync(
            "b@x.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(
            default, default, default, default, default);
        f.PasswordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
    }

    [Fact]
    public async Task ResendEmailChangeAsync_NothingPending_Throws409()
    {
        Fixture f = EmailFixture(out _);

        ConflictException ex = await Assert.ThrowsAsync<ConflictException>(
            () => f.Service.ResendEmailChangeAsync(CancellationToken.None));

        Assert.Equal(ApplicationMessages.EmailChangeNotPending, ex.Message);
        Assert.Empty(f.Tokens);
        await f.EmailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task CancelEmailChangeAsync_ClearsPendingAndConsumesChangeTokens()
    {
        Fixture f = EmailFixture(out Account account, pending: "b@x.com");
        EmailVerificationToken change = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id, "h1", DateTime.UtcNow.AddHours(1), EmailVerificationPurpose.ChangeEmail, "b@x.com"));
        f.Tokens.Add(change);

        await f.Service.CancelEmailChangeAsync(CancellationToken.None);

        Assert.Null(account.PendingEmail);
        Assert.NotNull(change.ConsumedAt);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelEmailChangeAsync_NothingPending_Succeeds()
    {
        Fixture f = EmailFixture(out Account account);

        await f.Service.CancelEmailChangeAsync(CancellationToken.None);

        Assert.Null(account.PendingEmail);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_EmailChangedWithCorrectPassword_SetsPendingSendsLinkKeepsEmail()
    {
        Fixture f = EmailFixture(out Account account);

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None);

        Assert.Equal("a@x.com", dto.Email);
        Assert.Equal("b@x.com", dto.PendingEmail);
        Assert.Equal("a@x.com", account.Email);
        EmailVerificationToken token = Assert.Single(f.Tokens);
        Assert.Equal(EmailVerificationPurpose.ChangeEmail, token.Purpose);
        Assert.Equal("b@x.com", token.Email);
        await f.EmailSender.Received(1).SendAsync(
            "b@x.com",
            "Xác nhận đổi địa chỉ email",
            Arg.Is<string>(b => b != null && b.Contains("nva") && b.Contains("Nếu không phải bạn, hãy bỏ qua thư này")
                && b.Contains("/#/confirm-email-change?token=") && b.Contains("Link có hiệu lực trong 24 giờ.")),
            Arg.Any<CancellationToken>());
        await f.PasswordAttempts.Received(1).ClearAsync(
            PasswordAttemptPurpose.EmailChange, account.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCurrentProfileAsync_EmailChangedWithoutPassword_Throws400AndReservesNothing(string? password)
    {
        Fixture f = EmailFixture(out Account account);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            "FieldRequired",
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", password, "Changed"), CancellationToken.None));

        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(
            default, default, default, default, default);
        await f.EmailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        Assert.Equal("Nguyen Van A", account.Name);
        Assert.Null(account.PendingEmail);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_WrongPassword_Throws401AndSavesNothing()
    {
        Fixture f = EmailFixture(out Account account);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "bad", "Changed"), CancellationToken.None));

        Assert.Null(account.PendingEmail);
        Assert.Equal("Nguyen Van A", account.Name);
        Assert.Empty(f.Tokens);
        await f.EmailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceive().ClearAsync(
            Arg.Any<PasswordAttemptPurpose>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_WrongPasswordAtMaxAttempt_Locks()
    {
        Fixture f = EmailFixture(out Account account);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.EmailChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutSettings().MaxFailedAttempts);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "bad"), CancellationToken.None));

        await f.PasswordAttempts.Received(1).LockAsync(
            PasswordAttemptPurpose.EmailChange, account.Id, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_Locked_ThrowsTemporarilyLockedEvenWithCorrectPassword()
    {
        Fixture f = EmailFixture(out Account account);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.EmailChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.EmailChangeTemporarilyLocked,
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None));

        f.PasswordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        Assert.Null(account.PendingEmail);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_WrongPasswordAndAddressTaken_Throws401Not409()
    {
        Account other = CreateAccount(username: "other", email: "b@x.com");
        Fixture f = EmailFixture(out _, others: other);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "bad"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_AddressIsOtherAccountsEmail_Throws409()
    {
        Account other = CreateAccount(username: "other", email: "b@x.com");
        Fixture f = EmailFixture(out Account account, others: other);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.AccountEmailAlreadyExists,
            () => f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None));

        Assert.Null(account.PendingEmail);
        Assert.Empty(f.Tokens);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_AddressIsOtherAccountsPendingEmail_Succeeds()
    {
        Account other = CreateAccount(username: "other", email: "c@x.com");
        other.RequestEmailChange("b@x.com");
        Fixture f = EmailFixture(out Account account, others: other);

        await f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None);

        Assert.Equal("b@x.com", account.PendingEmail);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_NoPasswordAccount_ChangesWithoutAsking()
    {
        Fixture f = EmailFixture(out Account account, passwordHash: null);

        await f.Service.UpdateCurrentProfileAsync(Req("b@x.com", null), CancellationToken.None);

        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(
            default, default, default, default, default);
        Assert.Equal("b@x.com", account.PendingEmail);
        await f.EmailSender.Received(1).SendAsync("b@x.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_EmailUnchangedIgnoringCaseAndSpaces_NoPasswordNoLink()
    {
        Fixture f = EmailFixture(out Account account);

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(Req("  A@X.com ", null, "New"), CancellationToken.None);

        Assert.Equal("New", dto.Name);
        Assert.Null(account.PendingEmail);
        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(
            default, default, default, default, default);
        await f.EmailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        Assert.Empty(f.Tokens);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_EmailEqualsPending_NoNewLinkOldLinkKept()
    {
        Fixture f = EmailFixture(out Account account, pending: "b@x.com");
        EmailVerificationToken old = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id, "hash", DateTime.UtcNow.AddHours(1), EmailVerificationPurpose.ChangeEmail, "b@x.com"));
        f.Tokens.Add(old);

        await f.Service.UpdateCurrentProfileAsync(Req("B@x.com", null), CancellationToken.None);

        Assert.Equal("b@x.com", account.PendingEmail);
        Assert.Null(old.ConsumedAt);
        Assert.Single(f.Tokens);
        await f.EmailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(
            default, default, default, default, default);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_ThirdAddressWhilePending_AsksPasswordAgainAndConsumesOldLink()
    {
        Fixture f = EmailFixture(out Account account, pending: "b@x.com");
        EmailVerificationToken old = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id, "hash", DateTime.UtcNow.AddHours(1), EmailVerificationPurpose.ChangeEmail, "b@x.com"));
        f.Tokens.Add(old);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            "FieldRequired",
            () => f.Service.UpdateCurrentProfileAsync(Req("c@x.com", null), CancellationToken.None));
        Assert.Null(old.ConsumedAt);

        await f.Service.UpdateCurrentProfileAsync(Req("c@x.com", "pw"), CancellationToken.None);

        Assert.Equal("c@x.com", account.PendingEmail);
        Assert.NotNull(old.ConsumedAt);
        Assert.Equal(2, f.Tokens.Count);
        Assert.Equal("c@x.com", f.Tokens[1].Email);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_SendFails_StillSavesPendingAndLogsOnlyAccountId()
    {
        Fixture f = EmailFixture(out Account account);
        f.EmailSender.SendAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("smtp down")));

        ProfileDto dto = await f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None);

        Assert.Equal("b@x.com", dto.PendingEmail);
        Assert.Single(f.Tokens);
        ICall call = Assert.Single(f.LinkLogger.ReceivedCalls(), c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Error);
        string state = call.GetArguments()[2]!.ToString()!;
        Assert.Contains(account.Id.ToString(), state);
        Assert.DoesNotContain("b@x.com", state);
        Assert.DoesNotContain(f.Tokens[0].TokenHash, state);
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_LoginAndPasswordChangeCountersUntouched()
    {
        Fixture f = EmailFixture(out Account account);

        await f.Service.UpdateCurrentProfileAsync(Req("b@x.com", "pw"), CancellationToken.None);

        await f.PasswordAttempts.DidNotReceive().ReserveAttemptAsync(
            PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceive().ReserveAttemptAsync(
            PasswordAttemptPurpose.PasswordChange, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceive().ClearAsync(
            Arg.Is<PasswordAttemptPurpose>(p => p != PasswordAttemptPurpose.EmailChange), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, account.FailedLoginCount);
        Assert.Equal(0, account.FailedPasswordChangeCount);
    }

    // ChangePasswordAsync

    [Fact]
    public async Task ChangePasswordAsync_NoPasswordHash_ThrowsUnauthorized_AndDoesNotCallVerify()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount(passwordHash: null);
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        ChangePasswordRequest request = new("current123", "newpassword123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(request, CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().ReserveAttemptAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        ChangePasswordRequest request = new("wrong-password", "newpassword123");

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_HashesAndSavesNewPassword()
    {
        Fixture f = CreateFixture();
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        f.PasswordHasher.Hash("newpassword123").Returns("new-hashed-password");
        ChangePasswordRequest request = new("current123", "newpassword123");

        await f.Service.ChangePasswordAsync(request, CancellationToken.None);

        Assert.Equal("new-hashed-password", account.PasswordHash);
        f.AccountRepo.Received(1).Update(account);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Reported before the check, like sign-in: refusing only a correct password would confirm the guess.
    [Fact]
    public async Task ChangePasswordAsync_Locked_RefusesWithTheLockMessage_WithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.PasswordChangeTemporarilyLocked,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_AttemptBeyondTheLimit_IsRefusedWithoutCheckingThePassword()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(6);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.PasswordChangeTemporarilyLocked,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None));

        f.PasswordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    // Its own counter: a stolen session guessing here must not lock the owner out of signing in.
    [Fact]
    public async Task ChangePasswordAsync_WrongPasswordOnTheLastAllowedAttempt_LocksPasswordChangeOnly()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordAttempts.ReserveAttemptAsync(
                PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<DateTime>(), TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(5);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        DateTime before = DateTime.UtcNow;
        using CancellationTokenSource aborted = new();
        await aborted.CancelAsync();

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.InvalidCurrentPassword,
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("wrong-password", "newpassword123"), aborted.Token));

        await f.PasswordAttempts.Received(1).LockAsync(
            PasswordAttemptPurpose.PasswordChange,
            account.Id,
            Arg.Is<DateTime>(until => until >= before.AddMinutes(15) && until <= DateTime.UtcNow.AddMinutes(15)),
            Arg.Is<CancellationToken>(token => !token.CanBeCanceled));
        await f.PasswordAttempts.DidNotReceive().ReserveAttemptAsync(
            PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongPasswordBelowTheLimit_DoesNotLock()
    {
        Fixture f = CreateFixture();
        ArrangeAccount(f);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Service.ChangePasswordAsync(new ChangePasswordRequest("wrong-password", "newpassword123"), CancellationToken.None));

        await f.PasswordAttempts.DidNotReceiveWithAnyArgs().LockAsync(default, default, default, default);
    }

    [Fact]
    public async Task ChangePasswordAsync_CorrectPassword_ClearsThePasswordChangeCounter()
    {
        Fixture f = CreateFixture();
        Account account = ArrangeAccount(f);
        f.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        f.PasswordHasher.Hash(Arg.Any<string>()).Returns("new-hashed-password");

        await f.Service.ChangePasswordAsync(new ChangePasswordRequest("current123", "newpassword123"), CancellationToken.None);

        await f.PasswordAttempts.Received(1).ClearAsync(
            PasswordAttemptPurpose.PasswordChange, account.Id, Arg.Any<CancellationToken>());
        await f.PasswordAttempts.DidNotReceive().ClearAsync(
            PasswordAttemptPurpose.Login, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Account ArrangeAccount(Fixture f)
    {
        Account account = CreateAccount();
        f.CurrentUserService.UserId.Returns(account.Id.ToString());
        f.AccountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        return account;
    }
}
