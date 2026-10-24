using System.ComponentModel.DataAnnotations;
using FeedbackHub.Application.Common;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Invitations;

/// <summary>Registration through an invitation link. There is no email: the account takes the invitation's.</summary>
/// <param name="Token">The code from the invitation link; only its hash is ever looked up or stored.</param>
public sealed record RegisterWithInvitationRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Token,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Username,
    [Required(ErrorMessage = "FieldRequired"), MinLength(PasswordPolicy.MinLength, ErrorMessage = "FieldMinLength"), MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "FieldMaxLength")] string Password,
    [MaxLength(20, ErrorMessage = "FieldMaxLength")] string? Phone = null,
    [MaxLength(100, ErrorMessage = "FieldMaxLength")] string? Position = null,
    [MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Address = null)
{
    internal AccountParams ToParams(string email) =>
        new(Name, Username, email, Phone, Position, Address);
}
