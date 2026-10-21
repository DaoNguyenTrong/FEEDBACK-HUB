using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Roles;

public sealed record CreateRoleRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Name,
    IReadOnlyList<string> PermissionCodes);
