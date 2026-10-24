using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Organizations;

public sealed record MemberRow(Guid MemberId, Guid AccountId, string AccountName, string Email);

public interface IOrganizationMemberQueries
{
    /// <summary>Active members of the organization, ordered by account name ignoring case and accents, then by id.</summary>
    Task<PagedResult<MemberRow>> ListActiveAsync(
        Guid organizationId, PageRequest request, CancellationToken cancellationToken);
}
