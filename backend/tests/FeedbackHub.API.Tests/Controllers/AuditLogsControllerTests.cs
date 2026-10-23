using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.ApiKeys;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class AuditLogsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
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
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"audit-caller-{Guid.NewGuid():N}", email: $"audit-caller-{Guid.NewGuid():N}@example.com");
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

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RealMutation_ProducesAuditLogEntry()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/admin/api-keys", new CreateApiKeyRequest("Audited Key"));
        CreateApiKeyResult? created = await createResponse.Content.ReadJsonAsync<CreateApiKeyResult>();

        HttpResponseMessage listResponse = await client.GetAsync("/api/admin/audit-logs?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        PagedResult<AuditLogDto>? auditLogs = await listResponse.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        Assert.Contains(auditLogs!.Items, log =>
            log.EntityName == "ApiKey" && log.EntityId == created!.Key.Id.ToString() && log.Action == "Added");
    }

    [Fact]
    public async Task GetAll_UserIdFilter_ScopesToThatCaller()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        Guid unrelatedUserId = Guid.NewGuid();

        HttpResponseMessage response = await client.GetAsync($"/api/admin/audit-logs?userId={unrelatedUserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<AuditLogDto>? auditLogs = await response.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        Assert.Empty(auditLogs!.Items);
    }

    [Fact]
    public async Task GetAll_ScopedToActiveOrganization_ExcludesOtherOrganizationsLogs()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        (HttpClient otherClient, _) = await CreateAuthedClientAsync();
        HttpResponseMessage otherCreateResponse = await otherClient.PostAsJsonAsync(
            "/api/admin/api-keys", new CreateApiKeyRequest("Other Org Key"));
        CreateApiKeyResult? otherCreated = await otherCreateResponse.Content.ReadJsonAsync<CreateApiKeyResult>();

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<AuditLogDto>? auditLogs = await response.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        Assert.DoesNotContain(auditLogs!.Items, log => log.EntityId == otherCreated!.Key.Id.ToString());
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Found_ReturnsAuditLog()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        await client.PostAsJsonAsync("/api/admin/api-keys", new CreateApiKeyRequest("GetById Audited Key"));

        HttpResponseMessage listResponse = await client.GetAsync("/api/admin/audit-logs?pageSize=100");
        PagedResult<AuditLogDto>? auditLogs = await listResponse.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        long targetId = auditLogs!.Items.First(l => l.EntityName == "ApiKey" && l.Action == "Added").Id;

        HttpResponseMessage response = await client.GetAsync($"/api/admin/audit-logs/{targetId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditLogDto? dto = await response.Content.ReadJsonAsync<AuditLogDto>();
        Assert.Equal(targetId, dto!.Id);
    }

    // Organization gate

    [Fact]
    public async Task GetById_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_MemberWithoutAuditLogsView_Returns403AccessDenied()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
    }
}
