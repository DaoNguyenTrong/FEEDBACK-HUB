using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Organizations;

public interface IMemberService
{
    /// <summary>Members of the working organization with per-row permissions for the caller (TECH00 §4.6).</summary>
    Task<PagedResult<OrganizationMemberDto>> ListAsync(PageRequest request, CancellationToken cancellationToken);
}
