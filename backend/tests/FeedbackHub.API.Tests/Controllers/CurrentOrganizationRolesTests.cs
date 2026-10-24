using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CurrentOrganizationRolesTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string OptionsPath = "/api/organization/role-options";
    private const string RolesPath = "/api/organization/roles";

    public static TheoryData<string> Paths => new() { OptionsPath, RolesPath };

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task RoleReads_Anonymous_Return401(string path)
    {
        HttpResponseMessage response = await fixture.CreateTestClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task RoleReads_ApiKey_Returns403AccessDenied(string path)
    {
        string rawKey;
        using (IServiceScope scope = fixture.Services.CreateScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "roles-api-key");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync(path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // BR-77
    [Fact]
    public async Task RoleOptions_WithoutMembersManage_Returns403()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync(OptionsPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // BR-77
    [Fact]
    public async Task Roles_WithoutRolesManage_Returns403()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync(RolesPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task RoleReads_PersonalContext_Returns403AccessDenied(string path)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, personalContext: true);

        HttpResponseMessage response = await client.GetAsync(path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // AC-311
    [Fact]
    public async Task RoleOptions_Response_HasNoPermissionCodes()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Admin);

        HttpResponseMessage response = await client.GetAsync(OptionsPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("permission", raw, StringComparison.OrdinalIgnoreCase);
        JsonElement[] options = JsonDocument.Parse(raw).RootElement.EnumerateArray().ToArray();
        Assert.Equal(["Owner", "Admin", "Member"], options.Select(option => option.GetProperty("name").GetString()));
        Assert.Equal([false, true, true], options.Select(option => option.GetProperty("canGrant").GetBoolean()));
    }

    // AC-248, AC-257
    [Fact]
    public async Task Roles_Owner_ReturnsSystemRolesWithFlagsAndHolderCounts()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Owner);

        HttpResponseMessage response = await client.GetAsync(RolesPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] roles = (await response.Content.ReadJsonElementAsync()).EnumerateArray().ToArray();
        Assert.Equal(["Owner", "Admin", "Member"], roles.Select(role => role.GetProperty("name").GetString()));
        Assert.Equal([1, 0, 0], roles.Select(role => role.GetProperty("holderCount").GetInt32()));
        Assert.All(roles, role => Assert.True(role.GetProperty("isSystem").GetBoolean()));
        Assert.All(roles, role => Assert.False(role.GetProperty("canEdit").GetBoolean()));
        Assert.All(roles, role => Assert.False(role.GetProperty("canDelete").GetBoolean()));
        Assert.Equal("SystemRole", roles[0].GetProperty("deleteBlockedReason").GetString());
        Assert.False(roles[0].TryGetProperty("pendingInvitationCount", out _));
    }
}
