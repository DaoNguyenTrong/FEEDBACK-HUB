using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Notifications;

public sealed record RegisterPushSubscriptionRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(4096, ErrorMessage = "FieldMaxLength")] string Token,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(20, ErrorMessage = "FieldMaxLength")] string Platform);
