using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Organizations;

public interface IMemberService
{
    /// <summary>Members of the working organization with per-row permissions for the caller (TECH00 §4.6).</summary>
    Task<PagedResult<OrganizationMemberDto>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces the member's organization roles; the same set again changes and records nothing.</summary>
    /// <remarks>Refuses with 400 (no role), 404 (member or a role outside the organization), 403 (BR-58) or 409 (no
    /// active Owner would be left), in that order.</remarks>
    Task ChangeRolesAsync(Guid accountId, UpdateMemberRolesRequest request, CancellationToken cancellationToken);

    /// <remarks>Refuses with 404 (no such active member), 403 (BR-58), then 409 (the caller themself, or the last
    /// active Owner).</remarks>
    Task RemoveAsync(Guid accountId, CancellationToken cancellationToken);

    /// <remarks>Refuses with 409 when the caller is the last active Owner.</remarks>
    Task LeaveAsync(CancellationToken cancellationToken);
}
