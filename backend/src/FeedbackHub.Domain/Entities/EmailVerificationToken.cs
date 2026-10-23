using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record EmailVerificationTokenParams(
    Guid AccountId,
    string TokenHash,
    DateTime ExpiresAt,
    EmailVerificationPurpose Purpose,
    string Email);

public sealed class EmailVerificationToken : BaseEntity<Guid>
{
    private EmailVerificationToken()
    {
    }

    public static EmailVerificationToken Create(EmailVerificationTokenParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.TokenHash))
        {
            throw new DomainException(DomainMessages.EmailVerificationTokenRequired);
        }

        if (p.ExpiresAt <= DateTime.UtcNow)
        {
            throw new DomainException(DomainMessages.EmailVerificationTokenExpiryFuture);
        }

        if (string.IsNullOrWhiteSpace(p.Email))
        {
            throw new DomainException(DomainMessages.AccountEmailRequired);
        }

        return new EmailVerificationToken
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            TokenHash = p.TokenHash.Trim(),
            ExpiresAt = p.ExpiresAt,
            Purpose = p.Purpose,
            Email = Account.NormalizeEmail(p.Email)
        };
    }

    public Guid AccountId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    /// <summary><c>null</c> only on rows created before purposes existed.</summary>
    public EmailVerificationPurpose? Purpose { get; private set; }

    /// <summary>The address this token confirms; <c>null</c> only on rows created before it was stored.</summary>
    public string? Email { get; private set; }

    public bool IsActiveAt(DateTime utcNow) => !ConsumedAt.HasValue && utcNow < ExpiresAt;

    public void Consume()
    {
        if (!ConsumedAt.HasValue)
        {
            ConsumedAt = DateTime.UtcNow;
        }
    }
}
