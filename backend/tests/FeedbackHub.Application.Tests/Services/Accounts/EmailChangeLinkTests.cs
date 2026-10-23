using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Accounts;

public class EmailChangeLinkTests
{
    private sealed record Fixture(EmailChangeLink Link, List<EmailVerificationToken> Tokens, IUnitOfWork Uow);

    private static Fixture CreateFixture()
    {
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        List<EmailVerificationToken> tokens = [];
        IRepository<EmailVerificationToken, Guid> repo = Substitute.For<IRepository<EmailVerificationToken, Guid>>();
        uow.Repository<EmailVerificationToken, Guid>().Returns(repo);
        RepositoryPredicateStub.StubListAsync(repo, tokens);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTime.UtcNow);
        EmailChangeLink link = new(
            uow,
            Substitute.For<IEmailSender>(),
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://app.example.com" }),
            clock,
            NullLogger<EmailChangeLink>.Instance);
        return new Fixture(link, tokens, uow);
    }

    private static EmailVerificationToken Token(Guid accountId, EmailVerificationPurpose purpose) =>
        EmailVerificationToken.Create(new EmailVerificationTokenParams(
            accountId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1), purpose, "x@x.com"));

    [Fact]
    public void Lifetime_Always_Is24Hours() => Assert.Equal(TimeSpan.FromHours(24), EmailChangeLink.Lifetime);

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(EmailVerificationPurpose.ChangeEmail, true, false)]
    [InlineData(EmailVerificationPurpose.VerifyEmail, false, true)]
    public async Task ConsumeAllAsync_PurposeGiven_ConsumesMatchingTokensWithoutSaving(
        EmailVerificationPurpose? purpose, bool changeConsumed, bool verifyConsumed)
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        EmailVerificationToken change = Token(accountId, EmailVerificationPurpose.ChangeEmail);
        EmailVerificationToken verify = Token(accountId, EmailVerificationPurpose.VerifyEmail);
        EmailVerificationToken otherAccount = Token(Guid.NewGuid(), EmailVerificationPurpose.ChangeEmail);
        f.Tokens.AddRange([change, verify, otherAccount]);

        await f.Link.ConsumeAllAsync(accountId, purpose, CancellationToken.None);

        Assert.Equal(changeConsumed, change.ConsumedAt is not null);
        Assert.Equal(verifyConsumed, verify.ConsumedAt is not null);
        Assert.Null(otherAccount.ConsumedAt);
        await f.Uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
