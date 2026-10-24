using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="RoleIds">The member's whole new set of organization roles; at least one, duplicates count once.</param>
public sealed record UpdateMemberRolesRequest([Required(ErrorMessage = "FieldRequired")] IReadOnlyList<Guid> RoleIds);
