using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <param name="PendingInvitationCount"><c>null</c> unless the caller may also manage members (BR-75).</param>
public sealed record RoleDto(
    Guid Id,
    string Name,
    SystemRoleKind? SystemKind,
    bool IsSystem,
    IReadOnlyList<string> PermissionCodes,
    int HolderCount,
    int? PendingInvitationCount,
    bool CanEdit,
    bool CanDelete,
    RoleDeleteBlockedReason? DeleteBlockedReason);
