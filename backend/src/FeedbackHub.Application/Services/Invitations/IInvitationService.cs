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

    /// <summary>
    /// What the link shows, without using it up: the first BR-81 check that fails, for the signed-in account if there
    /// is one. An API key or no session counts as anonymous, which sees <c>Valid</c> even for a suspended organization.
    /// </summary>
    Task<InvitationLookupDto> LookupAsync(InvitationTokenRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Joins the signed-in account to the invitation's organization with its role and uses the invitation up.
    /// An account already an active member gets 200 with no change.
    /// </summary>
    /// <remarks>Refuses in the BR-81 order: 409 <c>InvitationNoLongerValid</c>, <c>InvitationExpired</c>, <c>InvitationWrongAccount</c>, 403 <c>EmailNotConfirmed</c>, 409 <c>InvitationOrganizationSuspended</c>.</remarks>
    Task<AcceptInvitationResult> AcceptAsync(InvitationTokenRequest request, CancellationToken cancellationToken);
}
