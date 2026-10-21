using Microsoft.AspNetCore.Authorization;

namespace FeedbackHub.Infrastructure.Services.Auth;

internal sealed class OrganizationPermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
