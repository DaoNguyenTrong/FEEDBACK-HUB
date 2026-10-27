using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Invitations;

/// <param name="Email">Trimmed and lower-cased before use; an address with an invitation already has it replaced.</param>
/// <param name="RoleId">A role of the working organization; a missing or unknown id answers 404.</param>
public sealed record SendInvitationRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email,
    [Required(ErrorMessage = "FieldRequired")] Guid RoleId);
