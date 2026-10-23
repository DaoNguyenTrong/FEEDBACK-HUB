using Microsoft.AspNetCore.Authorization;
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
}
