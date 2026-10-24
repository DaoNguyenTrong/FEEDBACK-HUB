using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

public sealed record RoleDto(
    Guid Id,
    string Name,
    SystemRoleKind? SystemKind,
    bool IsSystem,
    IReadOnlyList<string> PermissionCodes,
    int HolderCount,
    bool CanEdit,
    bool CanDelete,
    RoleDeleteBlockedReason? DeleteBlockedReason);
