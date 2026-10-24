using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <param name="PermissionCodes">Effective codes: the Owner row carries every permission.</param>
/// <param name="HolderCount">Active members holding the role (BR-60).</param>
public sealed record RoleRow(
    Guid Id, string Name, SystemRoleKind? SystemKind, IReadOnlyList<string> PermissionCodes, int HolderCount);
