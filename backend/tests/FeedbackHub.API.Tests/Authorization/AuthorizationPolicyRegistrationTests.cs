using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.API.Tests.Authorization;

[Collection(nameof(ApiCollection))]
public sealed class AuthorizationPolicyRegistrationTests(ApiFactoryFixture fixture)
{
    // GetPolicyAsync

    [Theory]
    [InlineData(AuthorizationPolicies.ActiveOrganizationMember, DenyMode.Forbidden)]
    [InlineData(AuthorizationPolicies.ActiveOrganizationMemberOrNotFound, DenyMode.NotFound)]
    public async Task GetPolicyAsync_OrganizationContextPolicy_BuildsGateWithoutPermission(
        string policyName, DenyMode expectedDenyMode)
    {
        IAuthorizationPolicyProvider provider = fixture.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? policy = await provider.GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        OrganizationContextRequirement requirement =
            Assert.IsType<OrganizationContextRequirement>(Assert.Single(policy.Requirements));
        Assert.Null(requirement.PermissionCode);
        Assert.Equal(expectedDenyMode, requirement.DenyMode);
    }

    // An API key belongs to an integration, not a person: the policy authenticates the JWT scheme only, so it gets 401.
    [Fact]
    public async Task GetPolicyAsync_SystemAdministrator_JwtOnlyWithSystemAdministratorRequirement()
    {
        IAuthorizationPolicyProvider provider = fixture.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        AuthorizationPolicy? policy = await provider.GetPolicyAsync(AuthorizationPolicies.SystemAdministrator);

        Assert.NotNull(policy);
        Assert.Equal([JwtBearerDefaults.AuthenticationScheme], policy.AuthenticationSchemes);
        Assert.Equal(2, policy.Requirements.Count);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(policy.Requirements, r => r is SystemAdministratorRequirement);
    }
}
