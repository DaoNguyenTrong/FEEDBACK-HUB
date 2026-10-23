using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.SystemOrganizations;

public interface ISystemOrganizationQueries
{
    /// <summary>Every organization, ordered by name ignoring case and accents, then by id.</summary>
    Task<PagedResult<SystemOrganizationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken);
}
