namespace FeedbackHub.Application.Services.Invitations;

public interface IInvitationService
{
    /// <summary>
    /// Invites the address into the working organization, replacing its earlier invitation, and mails the link.
    /// Nothing is kept when the mail cannot be sent (503 <c>InvitationEmailDeliveryFailed</c>).
    /// </summary>
    Task<InvitationDto> SendAsync(SendInvitationRequest request, CancellationToken cancellationToken);
}
