using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Organizations;

namespace FeedbackHub.Application.Services.SystemOrganizations;

public interface ISystemOrganizationService
{
    Task<PagedResult<SystemOrganizationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>Does nothing when the organization is already deactivated.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The reason is blank.</exception>
    /// <exception cref="Domain.Exceptions.NotFoundException">No such organization.</exception>
    Task DeactivateAsync(Guid organizationId, DeactivateOrganizationRequest request, CancellationToken cancellationToken);

    /// <summary>Does nothing when the organization is active.</summary>
    /// <exception cref="Domain.Exceptions.NotFoundException">No such organization.</exception>
    Task ReactivateAsync(Guid organizationId, CancellationToken cancellationToken);
}
