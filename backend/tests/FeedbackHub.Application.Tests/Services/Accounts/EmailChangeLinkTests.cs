using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Application.Resources;

namespace FeedbackHub.Application.Tests.Services.Accounts;

public class EmailChangeLinkTests
{
    private sealed record Fixture(
        EmailChangeLink Link,
        List<EmailVerificationToken> Tokens,
        IUnitOfWork Uow,
        IEmailSender Sender,
        IRepository<Account, Guid> Accounts,
        IRepository<EmailVerificationToken, Guid> TokenRepo);

    private static Fixture CreateFixture()
    {
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        List<EmailVerificationToken> tokens = [];
        IRepository<EmailVerificationToken, Guid> repo = Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        uow.Repository<EmailVerificationToken, Guid>().Returns(repo);
        RepositoryPredicateStub.StubListAsync(repo, tokens);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);
        IRepository<Account, Guid> accounts = Substitute.For<IRepository<Account, Guid>>();
        uow.Repository<Account, Guid>().Returns(accounts);
        IEmailSender sender = Substitute.For<IEmailSender>();
        EmailChangeLink link = new(
            uow,
            sender,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.example.com" }),
            clock,
            NullLogger<EmailChangeLink>.Instance);
        return new Fixture(link, tokens, uow, sender, accounts, repo);
    }

    private static EmailVerificationToken Token(Guid accountId, EmailVerificationPurpose purpose) =>
        EmailVerificationToken.Create(new EmailVerificationTokenParams(
            accountId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1), purpose, "x@x.com"));

    private static Account StageOneAccount()
    {
        Account account = Account.Create(new AccountParams("Nguyen <Van> A", "nva", "old@example.com"));
        account.ConfirmEmail();
        account.RequestEmailChange("new&co@example.com");
        return account;
    }

    private static Account StageTwoAccount()
    {
        Account account = Account.Create(new AccountParams("Nguyen Van A", "nva", "old@example.com"));
        account.SetPasswordHash("hash");
        account.RequestEmailChange("new@example.com");
        return account;
    }

    private static EmailVerificationToken TokenCreatedAt(Guid accountId, EmailVerificationPurpose purpose, DateTime createdAt)
    {
        EmailVerificationToken token = Token(accountId, purpose);
        token.CreatedAt = createdAt;
        return token;
    }

    [Fact]
    public void Lifetime_Always_Is24Hours() => Assert.Equal(TimeSpan.FromHours(24), EmailChangeLink.Lifetime);

    [Fact]
    public async Task CreateAsync_StageOne_CreatesConfirmOldEmailTokenForPendingAddress_DoesNotSave()
    {
        Fixture f = CreateFixture();
        Account account = StageOneAccount();
        EmailVerificationToken? added = null;
        await f.TokenRepo.AddAsync(Arg.Do<EmailVerificationToken>(t => added = t), Arg.Any<CancellationToken>());

        string raw = await f.Link.CreateAsync(account, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(raw));
        Assert.NotNull(added);
        Assert.Equal(EmailVerificationPurpose.ConfirmOldEmail, added.Purpose);
        Assert.Equal("new&co@example.com", added.Email);
        Assert.Equal(account.Id, added.AccountId);
        Assert.NotEqual(raw, added.TokenHash);
        f.Accounts.Received(1).Update(account);
        await f.Uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_StageTwo_CreatesChangeEmailToken()
    {
        Fixture f = CreateFixture();
        Account account = StageTwoAccount();
        EmailVerificationToken? added = null;
        await f.TokenRepo.AddAsync(Arg.Do<EmailVerificationToken>(t => added = t), Arg.Any<CancellationToken>());

        await f.Link.CreateAsync(account, CancellationToken.None);

        Assert.Equal(EmailVerificationPurpose.ChangeEmail, added!.Purpose);
        Assert.Equal("new@example.com", added.Email);
    }

    [Fact]
    public async Task CreateAsync_ConsumesEarlierLinksOfBothPurposes_KeepsVerifyEmail()
    {
        Fixture f = CreateFixture();
        Account account = StageTwoAccount();
        EmailVerificationToken change = Token(account.Id, EmailVerificationPurpose.ChangeEmail);
        EmailVerificationToken old = Token(account.Id, EmailVerificationPurpose.ConfirmOldEmail);
        EmailVerificationToken verify = Token(account.Id, EmailVerificationPurpose.VerifyEmail);
        f.Tokens.AddRange([change, old, verify]);

        await f.Link.CreateAsync(account, CancellationToken.None);

        Assert.NotNull(change.ConsumedAt);
        Assert.NotNull(old.ConsumedAt);
        Assert.Null(verify.ConsumedAt);
    }

    [Fact]
    public async Task SendAsync_StageOne_MailsCurrentAddressOnly()
    {
        Fixture f = CreateFixture();
        Account account = StageOneAccount();

        await f.Link.SendAsync(account, "raw-token", CancellationToken.None);

        IEmailSender sender = f.Sender;
        await sender.Received(1).SendAsync(
            "old@example.com",
            "Xác nhận yêu cầu đổi địa chỉ email",
            Arg.Is<string>(b =>
                b.Contains("nva")
                && b.Contains("new&amp;co@example.com")
                && !b.Contains("new&co@example.com")
                && b.Contains("Nguyen &lt;Van&gt; A")
                && b.Contains("Nếu bạn bấm Xác nhận trên trang này, tài khoản sẽ chuyển sang địa chỉ new&amp;co@example.com.")
                && b.Contains("/#/confirm-current-email?token=raw-token")
                && b.Contains("Link có hiệu lực trong 24 giờ.")
                && b.Contains("Nếu không phải bạn, hãy bỏ qua thư này.")),
            Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendAsync(
            "new&co@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_StageTwo_MailsPendingAddressOnly()
    {
        Fixture f = CreateFixture();
        Account account = StageTwoAccount();

        await f.Link.SendAsync(account, "raw-token", CancellationToken.None);

        await f.Sender.Received(1).SendAsync(
            "new@example.com",
            "Xác nhận đổi địa chỉ email",
            Arg.Is<string>(b => b.Contains("/#/confirm-email-change?token=raw-token")),
            Arg.Any<CancellationToken>());
        await f.Sender.DidNotReceive().SendAsync(
            "old@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SenderThrows_LogsAndDoesNotThrow()
    {
        Fixture f = CreateFixture();
        f.Sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));

        await f.Link.SendAsync(StageOneAccount(), "raw-token", CancellationToken.None);
    }

    [Fact]
    public async Task EnsureUnderCapAsync_FiveChangeTokensInLastHour_Throws429Key()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.Tokens.AddRange([
            TokenCreatedAt(id, EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-1)),
            TokenCreatedAt(id, EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-2)),
            TokenCreatedAt(id, EmailVerificationPurpose.ConfirmOldEmail, DateTime.UtcNow.AddMinutes(-3)),
            TokenCreatedAt(id, EmailVerificationPurpose.ConfirmOldEmail, DateTime.UtcNow.AddMinutes(-4)),
            TokenCreatedAt(id, EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-59))]);
        f.Tokens[0].Consume();

        TooManyRequestsException ex = await Assert.ThrowsAsync<TooManyRequestsException>(
            () => f.Link.EnsureUnderCapAsync(id, CancellationToken.None));
        Assert.Equal(ApplicationMessages.EmailChangeTooManyRequests, ex.Message);
    }

    [Fact]
    public async Task EnsureUnderCapAsync_FourTokensInLastHour_Passes()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        for (int i = 0; i < 4; i++)
        {
            f.Tokens.Add(TokenCreatedAt(id, EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-i - 1)));
        }

        await f.Link.EnsureUnderCapAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureUnderCapAsync_FiveTokensOlderThanOneHour_Passes()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            f.Tokens.Add(TokenCreatedAt(id, EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-61 - i)));
        }

        await f.Link.EnsureUnderCapAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureUnderCapAsync_VerifyEmailTokensDoNotCount()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        for (int i = 0; i < 6; i++)
        {
            f.Tokens.Add(TokenCreatedAt(id, EmailVerificationPurpose.VerifyEmail, DateTime.UtcNow.AddMinutes(-1)));
        }

        await f.Link.EnsureUnderCapAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureUnderCapAsync_OtherAccountsTokensDoNotCount()
    {
        Fixture f = CreateFixture();
        for (int i = 0; i < 6; i++)
        {
            f.Tokens.Add(TokenCreatedAt(Guid.NewGuid(), EmailVerificationPurpose.ChangeEmail, DateTime.UtcNow.AddMinutes(-1)));
        }

        await f.Link.EnsureUnderCapAsync(Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task ConsumeAllAsync_PurposesGiven_ConsumesOnlyThose()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        EmailVerificationToken change = Token(accountId, EmailVerificationPurpose.ChangeEmail);
        EmailVerificationToken old = Token(accountId, EmailVerificationPurpose.ConfirmOldEmail);
        EmailVerificationToken verify = Token(accountId, EmailVerificationPurpose.VerifyEmail);
        f.Tokens.AddRange([change, old, verify]);

        await f.Link.ConsumeAllAsync(accountId, [EmailVerificationPurpose.ConfirmOldEmail], CancellationToken.None);

        Assert.Null(change.ConsumedAt);
        Assert.NotNull(old.ConsumedAt);
        Assert.Null(verify.ConsumedAt);
        await f.Uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeAllAsync_NullPurposes_ConsumesEveryPurposeOfThatAccountOnly()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        EmailVerificationToken change = Token(accountId, EmailVerificationPurpose.ChangeEmail);
        EmailVerificationToken old = Token(accountId, EmailVerificationPurpose.ConfirmOldEmail);
        EmailVerificationToken verify = Token(accountId, EmailVerificationPurpose.VerifyEmail);
        EmailVerificationToken other = Token(Guid.NewGuid(), EmailVerificationPurpose.ChangeEmail);
        f.Tokens.AddRange([change, old, verify, other]);

        await f.Link.ConsumeAllAsync(accountId, null, CancellationToken.None);

        Assert.NotNull(change.ConsumedAt);
        Assert.NotNull(old.ConsumedAt);
        Assert.NotNull(verify.ConsumedAt);
        Assert.Null(other.ConsumedAt);
        await f.Uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
