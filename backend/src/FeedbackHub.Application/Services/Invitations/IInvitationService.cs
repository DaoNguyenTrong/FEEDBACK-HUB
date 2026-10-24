using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Invitations;

public interface IInvitationService
{
    /// <summary>
    /// Invites the address into the working organization, replacing its earlier invitation, and mails the link.
    /// Nothing is kept when the mail cannot be sent (503 <c>InvitationEmailDeliveryFailed</c>).
    /// </summary>
    Task<InvitationDto> SendAsync(SendInvitationRequest request, CancellationToken cancellationToken);

    /// <summary>Pending and expired invitations; 403 <c>OrganizationAccessDenied</c> unless the database grants
    /// <c>organizations.members.manage</c>.</summary>
    Task<PagedResult<InvitationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the invitation with a new one to the same address and role (new id, fresh expiry) and mails it:
    /// 404 outside the organization, 403 BR-58, 409 <c>InvitationEmailAlreadyMember</c>, 503 as for sending.
    /// </summary>
    Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>Revokes a pending invitation or deletes an expired one, recorded either way: 404, 403 BR-58.</summary>
    Task RevokeOrDeleteAsync(Guid invitationId, CancellationToken cancellationToken);
}
