using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

public sealed record RoleGrantCaller(Guid AccountId, bool IsOwner, IReadOnlySet<string> Permissions);

public sealed record RoleGrantRole(Guid Id, string Name, SystemRoleKind? SystemKind, IReadOnlySet<string> Permissions);

public enum RoleLockReason
{
    OwnerOnly = 1,
    InsufficientRolePermissions = 2
}

/// <param name="Reason">Why the operation is refused.</param>
/// <param name="Role">The first role out of reach, in the order given; <c>null</c> for an Owner-only refusal.</param>
public sealed record RoleGrantDenial(RoleLockReason Reason, RoleGrantRole? Role);

/// <summary>
/// The only place BR-58 (anti-privilege-escalation, TECH00 §5.3) is computed, for server checks and DTO flags alike.
/// </summary>
public static class RoleGrantPolicy
{
    public static bool CanGrant(RoleGrantCaller caller, RoleGrantRole role) =>
        role.SystemKind == SystemRoleKind.Owner
            ? caller.IsOwner
            : role.Permissions.IsSubsetOf(caller.Permissions);

    public static bool CanTakeAway(RoleGrantCaller caller, RoleGrantRole role) => CanGrant(caller, role);

    /// <returns><c>null</c> when the caller may take away every role.</returns>
    public static RoleGrantDenial? CheckTakeAwayAll(RoleGrantCaller caller, IReadOnlyList<RoleGrantRole> roles) =>
        Check(caller, roles, CanTakeAway);

    /// <returns><c>null</c> when the caller may grant every role.</returns>
    public static RoleGrantDenial? CheckGrantAll(RoleGrantCaller caller, IReadOnlyList<RoleGrantRole> roles) =>
        Check(caller, roles, CanGrant);

    public static bool HasAllPermissions(RoleGrantCaller caller, IEnumerable<string> permissionCodes) =>
        permissionCodes.All(caller.Permissions.Contains);

    public static string ErrorCode(RoleGrantDenial denial) =>
        denial.Reason == RoleLockReason.OwnerOnly
            ? ApplicationMessages.OwnerOnlyOperation
            : ApplicationMessages.InsufficientRolePermissions;

    // The Owner reason wins over a missing permission so the lock reason and the 403 code agree (AC-341).
    private static RoleGrantDenial? Check(
        RoleGrantCaller caller, IReadOnlyList<RoleGrantRole> roles, Func<RoleGrantCaller, RoleGrantRole, bool> allowed)
    {
        if (!caller.IsOwner && roles.Any(role => role.SystemKind == SystemRoleKind.Owner))
        {
            return new RoleGrantDenial(RoleLockReason.OwnerOnly, null);
        }

        RoleGrantRole? firstDenied = roles.FirstOrDefault(role => !allowed(caller, role));

        return firstDenied is null
            ? null
            : new RoleGrantDenial(RoleLockReason.InsufficientRolePermissions, firstDenied);
    }
}
