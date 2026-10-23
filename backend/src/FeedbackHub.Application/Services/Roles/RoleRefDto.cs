using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <param name="SystemKind">Set for the Owner, Admin and Member roles, so the client can show their localized name.</param>
public sealed record RoleRefDto(Guid Id, string Name, SystemRoleKind? SystemKind)
{
    public static RoleRefDto From(Role role) => new(role.Id, role.Name, role.SystemRoleKind);
}
