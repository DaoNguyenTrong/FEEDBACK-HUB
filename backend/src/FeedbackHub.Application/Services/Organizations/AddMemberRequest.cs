using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Organizations;

public sealed record AddMemberRequest(
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email,
    IReadOnlyList<Guid> RoleIds);
