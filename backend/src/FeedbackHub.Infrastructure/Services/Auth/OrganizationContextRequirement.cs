using Microsoft.AspNetCore.Authorization;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>
/// The caller is an active member of the non-suspended organization in the JWT <c>org_id</c> claim and, when
/// <see cref="PermissionCode"/> is set, holds that permission there. Checked by
/// <see cref="OrganizationContextAuthorizationHandler"/>.
/// </summary>
public sealed class OrganizationContextRequirement(string? permissionCode, DenyMode denyMode) : IAuthorizationRequirement
{
    public string? PermissionCode { get; } = permissionCode;

    public DenyMode DenyMode { get; } = denyMode;
}
