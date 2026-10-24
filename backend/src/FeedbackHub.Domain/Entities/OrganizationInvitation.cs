using System.Text.RegularExpressions;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record OrganizationInvitationParams(
    Guid OrganizationId,
    string Email,
    Guid RoleId,
    string TokenHash,
    DateTime IssuedAt);

public sealed partial class OrganizationInvitation : BaseEntity<Guid>, IHasOrganization
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(168);

    private OrganizationInvitation()
    {
    }

    public static OrganizationInvitation Create(OrganizationInvitationParams p)
    {
        if (p.OrganizationId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.InvitationOrganizationRequired);
        }

        if (p.RoleId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.InvitationRoleRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Email))
        {
            throw new DomainException(DomainMessages.AccountEmailRequired);
        }

        if (p.TokenHash is null || !TokenHashPattern().IsMatch(p.TokenHash))
        {
            throw new DomainException(DomainMessages.InvitationTokenHashInvalid);
        }

        return new OrganizationInvitation
        {
            Id = IdGenerator.NewUuidV7(),
            OrganizationId = p.OrganizationId,
            Email = Account.NormalizeEmail(p.Email),
            RoleId = p.RoleId,
            TokenHash = p.TokenHash,
            ExpiresAt = p.IssuedAt + Lifetime
        };
    }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public Guid RoleId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public bool IsExpiredAt(DateTime utcNow) => ExpiresAt <= utcNow;

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex TokenHashPattern();
}
