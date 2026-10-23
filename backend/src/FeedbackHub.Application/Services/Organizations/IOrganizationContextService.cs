namespace FeedbackHub.Application.Services.Organizations;

/// <summary>The organization the caller is working in (the JWT's <c>org_id</c>), already let through by the gate.</summary>
public interface IOrganizationContextService
{
    Task<CurrentOrganizationDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>A name equal to the current one after trimming (case-sensitive) changes and records nothing.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The name is blank.</exception>
    Task<CurrentOrganizationDto> RenameAsync(RenameOrganizationRequest request, CancellationToken cancellationToken);

    /// <exception cref="Domain.Exceptions.DomainException">The caller is a system administrator and gave no reason.</exception>
    Task DeactivateAsync(DeactivateOrganizationRequest request, CancellationToken cancellationToken);
}
