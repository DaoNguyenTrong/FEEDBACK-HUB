using System.Text.RegularExpressions;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record OrganizationParams(string Name, string Slug);

public record OrganizationNameParams(string Name);

public record DeactivationParams(Guid ActorAccountId, bool BySystemAdministrator, string? Reason, DateTime DeactivatedAt);

public sealed partial class Organization : BaseEntity<Guid>
{
    private const int SlugMinLength = 3;
    private const int SlugMaxLength = 50;

    private Organization()
    {
    }

    public static Organization Create(OrganizationParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Slug))
        {
            throw new DomainException(DomainMessages.OrganizationSlugRequired);
        }

        string slug = p.Slug.Trim().ToLowerInvariant();
        if (slug.Length is < SlugMinLength or > SlugMaxLength || !SlugFormat().IsMatch(slug))
        {
            throw new DomainException(DomainMessages.OrganizationSlugInvalid);
        }

        Organization organization = new()
        {
            Id = IdGenerator.NewUuidV7(),
            Slug = slug
        };
        organization.Update(new OrganizationNameParams(p.Name));
        return organization;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool Status { get; private set; } = true;

    public DateTime? DeactivatedAt { get; private set; }

    public Guid? DeactivatedByAccountId { get; private set; }

    public bool DeactivatedBySystemAdministrator { get; private set; }

    public string? DeactivationReason { get; private set; }

    public void Update(OrganizationNameParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.OrganizationNameRequired);
        }

        Name = p.Name.Trim();
    }

    public void Deactivate(DeactivationParams p)
    {
        if (!Status)
        {
            return;
        }

        Status = false;
        DeactivatedAt = p.DeactivatedAt;
        DeactivatedByAccountId = p.ActorAccountId;
        DeactivatedBySystemAdministrator = p.BySystemAdministrator;
        DeactivationReason = string.IsNullOrWhiteSpace(p.Reason) ? null : p.Reason.Trim();
    }

    public void Reactivate()
    {
        Status = true;
        DeactivatedAt = null;
        DeactivatedByAccountId = null;
        DeactivatedBySystemAdministrator = false;
        DeactivationReason = null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugFormat();
}
