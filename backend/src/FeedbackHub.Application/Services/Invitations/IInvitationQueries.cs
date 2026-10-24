using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Invitations;

public sealed record InvitationRow(Guid Id, string Email, Guid RoleId, DateTime ExpiresAt);

public interface IInvitationQueries
{
    /// <summary>Pending and expired invitations of the organization, latest expiry first, then by id (TECH00 §4.8).</summary>
    Task<PagedResult<InvitationRow>> ListAsync(
        Guid organizationId, PageRequest request, CancellationToken cancellationToken);
}
