using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.ApiKeys;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class ApiKeysControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(HttpClient Client, Guid OrganizationId)> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"apikeys-caller-{Guid.NewGuid():N}", email: $"apikeys-caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));
        return (client, organization.Id);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_OrdersByCreatedAtDescending()
    {
        (HttpClient client, Guid organizationId) = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            (_, _) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organizationId, "Older Key");
            (_, _) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organizationId, "Newer Key");
        }

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ApiKeyDto>? keys = await response.Content.ReadJsonAsync<List<ApiKeyDto>>();
        int olderIndex = keys!.FindIndex(k => k.Name == "Older Key");
        int newerIndex = keys.FindIndex(k => k.Name == "Newer Key");
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task Create_Valid_Returns201WithRawKeyFormat()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/api-keys", new CreateApiKeyRequest("New CI Key"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CreateApiKeyResult? result = await response.Content.ReadJsonAsync<CreateApiKeyResult>();
        Assert.Matches(new Regex("^sk_[A-Za-z0-9_-]+$"), result!.RawKey);
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/api-keys", new CreateApiKeyRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_NotFound_Returns404()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/api-keys/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Found_KeyNoLongerAuthenticates()
    {
        (HttpClient client, Guid organizationId) = await CreateAuthedClientAsync();
        ApiKey apiKey;
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (apiKey, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organizationId, "Deactivate Target");
        }

        HttpResponseMessage deactivateResponse = await client.DeleteAsync($"/api/admin/api-keys/{apiKey.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        using HttpClient apiKeyClient = fixture.CreateTestClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        HttpResponseMessage authedResponse = await apiKeyClient.GetAsync("/api/admin/api-keys");
        Assert.Equal(HttpStatusCode.Unauthorized, authedResponse.StatusCode);
    }

    // Organization gate

    [Theory]
    [InlineData("POST", "/api/admin/api-keys")]
    [InlineData("DELETE", "/api/admin/api-keys/00000000-0000-0000-0000-000000000001")]
    public async Task AnyEndpoint_Unauthenticated_ReturnsUnauthorized(string method, string path)
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_MemberWithoutApiKeysManage_Returns403AccessDenied()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("organizationName", out _));
    }

    // AC-321: a suspended organization is reported before the missing permission.
    [Fact]
    public async Task GetAll_SuspendedOrganizationMemberWithoutPermission_Returns403Suspended()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Member, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.GetProperty("code").GetString());
        Assert.Equal(AuthTestHelper.GateOrganizationName, body.GetProperty("organizationName").GetString());
    }

    // AC-320: someone outside a suspended organization learns neither its name nor its state.
    [Fact]
    public async Task GetAll_NonMemberOfSuspendedOrganization_Returns403WithoutName()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, member: false, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("organizationName", out _));
        Assert.DoesNotContain(AuthTestHelper.GateOrganizationName, raw);
    }
}
