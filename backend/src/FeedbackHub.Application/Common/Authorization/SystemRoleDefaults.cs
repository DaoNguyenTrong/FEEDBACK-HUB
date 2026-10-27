using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Authorization;

/// <summary>
/// Seeded permission sets for system roles, keyed by <see cref="SystemRoleKind"/>.
/// Owner is intentionally absent — its permission set is resolved dynamically as
/// <see cref="Permissions.All"/> so it can never fall behind a permission added later.
/// </summary>
public static class SystemRoleDefaults
{
    public static readonly IReadOnlyDictionary<SystemRoleKind, IReadOnlyList<string>> PermissionsByKind =
        new Dictionary<SystemRoleKind, IReadOnlyList<string>>
        {
            [SystemRoleKind.Admin] = [Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin],
            [SystemRoleKind.Member] = []
        };

    /// <summary>The permissions a role grants: <see cref="Permissions.All"/> for Owner, its stored codes otherwise.</summary>
    public static IReadOnlyList<string> EffectivePermissionCodes(
        SystemRoleKind? kind, IReadOnlyList<string> storedCodes) =>
        kind == SystemRoleKind.Owner ? Permissions.All : storedCodes;
}
