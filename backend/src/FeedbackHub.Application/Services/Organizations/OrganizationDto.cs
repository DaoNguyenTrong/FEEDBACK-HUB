using FeedbackHub.Application.Services.Roles;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="Status"><c>false</c>: the organization is deactivated; it stays listed so the picker can show it.</param>
public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    bool Status,
    IReadOnlyList<string> MyRoleNames,
    IReadOnlyList<RoleRefDto> MyRoles,
    IReadOnlyList<string> MyPermissionCodes);
