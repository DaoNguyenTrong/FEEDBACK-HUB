using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <param name="PermissionCodes">Effective codes: the Owner row carries every permission.</param>
/// <param name="HolderCount">Active members holding the role (BR-60).</param>
/// <param name="PendingInvitationCount">Invitations of the role not yet expired at the time asked for.</param>
public sealed record RoleRow(
    Guid Id,
    string Name,
    SystemRoleKind? SystemKind,
    IReadOnlyList<string> PermissionCodes,
    int HolderCount,
    int PendingInvitationCount);
