using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Authorization;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>
/// Turns any <see cref="Permissions"/> code used as a policy name into an <see cref="OrganizationContextRequirement"/>
/// checked in the JWT's organization (no per-permission <c>AddPolicy</c>); other names use the default provider.
/// </summary>
internal sealed class OrganizationPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!Permissions.All.Contains(policyName))
        {
            return fallback.GetPolicyAsync(policyName);
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new OrganizationContextRequirement(policyName, DenyMode.Forbidden))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
