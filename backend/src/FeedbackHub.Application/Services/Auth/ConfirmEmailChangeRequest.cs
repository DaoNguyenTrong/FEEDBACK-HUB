using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Auth;

public sealed record ConfirmEmailChangeRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Token);
