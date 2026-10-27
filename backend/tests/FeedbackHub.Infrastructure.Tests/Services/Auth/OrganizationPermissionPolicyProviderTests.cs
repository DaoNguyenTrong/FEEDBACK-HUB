using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth;

public class OrganizationPermissionPolicyProviderTests
{
    private const string RegisteredPolicyName = "registered-policy";

    private sealed record Fixture(
        OrganizationPermissionPolicyProvider Provider,
        AuthorizationOptions Options);

    private static Fixture CreateFixture(Action<AuthorizationOptions>? configure = null)
    {
        AuthorizationOptions options = new();
        configure?.Invoke(options);

        OrganizationPermissionPolicyProvider provider = new(Options.Create(options));

        return new Fixture(provider, options);
    }

    public static TheoryData<string> EveryPermissionCode() => [.. Permissions.All];

    // GetPolicyAsync

    // QĐ-06: every code is checked in the JWT's organization; none may fall back to a route-scoped check.
    [Theory]
    [MemberData(nameof(EveryPermissionCode))]
    public async Task GetPolicyAsync_EveryPermissionCode_BuildsGateRequirementForbidden(string code)
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(code);

        Assert.NotNull(policy);
        IAuthorizationRequirement requirement = Assert.Single(policy.Requirements);
        OrganizationContextRequirement typed = Assert.IsType<OrganizationContextRequirement>(requirement);
        Assert.Equal(code, typed.PermissionCode);
        Assert.Equal(DenyMode.Forbidden, typed.DenyMode);
        Assert.Empty(policy.AuthenticationSchemes);
    }

    [Fact]
    public async Task GetPolicyAsync_StaticallyRegisteredPolicy_FallsThroughToOptions()
    {
        AuthorizationPolicy registered = new AuthorizationPolicyBuilder()
            .AddRequirements(new SystemAdministratorRequirement())
            .Build();
        Fixture f = CreateFixture(options => options.AddPolicy(RegisteredPolicyName, registered));

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(RegisteredPolicyName);

        Assert.Same(registered, policy);
    }

    [Fact]
    public async Task GetPolicyAsync_RegisteredOrganizationMemberPolicy_FallsThroughToOptions()
    {
        Fixture f = CreateFixture(options => options.AddPolicy(
            AuthorizationPolicies.ActiveOrganizationMember,
            policy => policy.AddRequirements(new OrganizationContextRequirement(null, DenyMode.Forbidden))));

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(AuthorizationPolicies.ActiveOrganizationMember);

        Assert.NotNull(policy);
        Assert.IsType<OrganizationContextRequirement>(Assert.Single(policy.Requirements));
    }

    [Theory]
    [InlineData("unknown.permission")]
    [InlineData("FILES.MANAGE")]
    [InlineData("files")]
    [InlineData("")]
    public async Task GetPolicyAsync_UnknownName_ReturnsNull(string policyName)
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetPolicyAsync(policyName);

        Assert.Null(policy);
    }

    // GetDefaultPolicyAsync

    [Fact]
    public async Task GetDefaultPolicyAsync_ReturnsOptionsDefaultPolicy()
    {
        AuthorizationPolicy defaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes("Bearer", "ApiKey")
            .Build();
        Fixture f = CreateFixture(options => options.DefaultPolicy = defaultPolicy);

        AuthorizationPolicy policy = await f.Provider.GetDefaultPolicyAsync();

        Assert.Same(defaultPolicy, policy);
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_NotConfigured_RequiresAuthenticatedUser()
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy policy = await f.Provider.GetDefaultPolicyAsync();

        Assert.IsType<DenyAnonymousAuthorizationRequirement>(Assert.Single(policy.Requirements));
    }

    // GetFallbackPolicyAsync

    [Fact]
    public async Task GetFallbackPolicyAsync_NotConfigured_ReturnsNull()
    {
        Fixture f = CreateFixture();

        AuthorizationPolicy? policy = await f.Provider.GetFallbackPolicyAsync();

        Assert.Null(policy);
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_Configured_ReturnsOptionsFallbackPolicy()
    {
        AuthorizationPolicy fallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        Fixture f = CreateFixture(options => options.FallbackPolicy = fallbackPolicy);

        AuthorizationPolicy? policy = await f.Provider.GetFallbackPolicyAsync();

        Assert.Same(fallbackPolicy, policy);
    }
}
