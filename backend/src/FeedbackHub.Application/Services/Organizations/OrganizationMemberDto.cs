using FeedbackHub.Application.Services.Roles;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="Email">Only on the caller's own row unless the caller holds <c>organizations.members.manage</c>.</param>
/// <param name="LockReason">Why the roles of this row cannot be changed or removed; <c>null</c> when they can.</param>
/// <param name="LockRole">The role out of the caller's reach; <c>null</c> for an Owner-only lock.</param>
public sealed record OrganizationMemberDto(
    Guid AccountId,
    string AccountName,
    string? Email,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<RoleRefDto> Roles,
    bool IsSelf,
    bool CanChangeRoles,
    bool CanRemove,
    RoleLockReason? LockReason,
    RoleRefDto? LockRole);
