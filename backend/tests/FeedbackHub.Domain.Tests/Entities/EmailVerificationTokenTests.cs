using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class EmailVerificationTokenTests
{
    private static EmailVerificationTokenParams ValidParams()
    {
        return new EmailVerificationTokenParams(
            AccountId: Guid.NewGuid(),
            TokenHash: "hashed-token-value",
            ExpiresAt: DateTime.UtcNow.AddHours(24),
            Purpose: EmailVerificationPurpose.ChangeEmail,
            Email: "A@X.com");
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFields()
    {
        EmailVerificationTokenParams p = ValidParams();

        EmailVerificationToken token = EmailVerificationToken.Create(p);

        Assert.Equal(p.AccountId, token.AccountId);
        Assert.Equal(p.TokenHash, token.TokenHash);
        Assert.Equal(p.ExpiresAt, token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        Assert.NotEqual(Guid.Empty, token.Id);
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsDomainException()
    {
        EmailVerificationTokenParams p = ValidParams() with { AccountId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.AccountIdRequired, () => EmailVerificationToken.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankTokenHash_ThrowsDomainException(string tokenHash)
    {
        EmailVerificationTokenParams p = ValidParams() with { TokenHash = tokenHash };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.EmailVerificationTokenRequired, () => EmailVerificationToken.Create(p));
    }

    [Fact]
    public void Create_WithPastExpiresAt_ThrowsDomainException()
    {
        EmailVerificationTokenParams p = ValidParams() with { ExpiresAt = DateTime.UtcNow.AddHours(-1) };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.EmailVerificationTokenExpiryFuture, () => EmailVerificationToken.Create(p));
    }

    [Fact]
    public void Create_TrimsTokenHash()
    {
        EmailVerificationTokenParams p = ValidParams() with { TokenHash = " hash " };

        EmailVerificationToken token = EmailVerificationToken.Create(p);

        Assert.Equal("hash", token.TokenHash);
    }

    [Fact]
    public void IsActive_OnFreshToken_IsTrue()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams());

        Assert.True(token.IsActiveAt(DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_AfterConsume_IsFalse()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams());

        token.Consume();

        Assert.False(token.IsActiveAt(DateTime.UtcNow));
    }

    [Fact]
    public void Consume_SetsConsumedAtNonNull()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams());

        token.Consume();

        Assert.NotNull(token.ConsumedAt);
    }

    [Fact]
    public void Consume_CalledTwice_KeepsConsumedAtNonNullAndDoesNotThrow()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams());

        token.Consume();
        DateTime? firstConsumedAt = token.ConsumedAt;
        token.Consume();

        Assert.NotNull(token.ConsumedAt);
        Assert.Equal(firstConsumedAt, token.ConsumedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyEmail_Throws(string email)
    {
        EmailVerificationTokenParams p = ValidParams() with { Email = email };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.AccountEmailRequired, () => EmailVerificationToken.Create(p));
    }

    [Fact]
    public void Create_StoresPurposeAndNormalizedEmail()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams() with { Email = " B@X.com " });

        Assert.Equal(EmailVerificationPurpose.ChangeEmail, token.Purpose);
        Assert.Equal("b@x.com", token.Email);
    }

    [Fact]
    public void IsActiveAt_AfterExpiry_IsFalse()
    {
        DateTime now = DateTime.UtcNow;
        EmailVerificationToken token = EmailVerificationToken.Create(
            ValidParams() with { ExpiresAt = now.AddHours(1) });

        Assert.False(token.IsActiveAt(now.AddHours(2)));
    }

    [Fact]
    public void IsActiveAt_Consumed_IsFalse()
    {
        EmailVerificationToken token = EmailVerificationToken.Create(ValidParams());
        token.Consume();

        Assert.False(token.IsActiveAt(DateTime.UtcNow));
    }
}
