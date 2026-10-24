using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Invitations;

/// <param name="Token">The code from the invitation link; only its hash is ever looked up or stored.</param>
public sealed record InvitationTokenRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Token);
