using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Common;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class SystemOrganizationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Reason = "Hết hợp đồng";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private static readonly Guid AnyOrganizationId = Guid.Parse("0199f1a4-0000-7000-8000-000000000001");

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", "/api/system/organizations" },
        { "POST", $"/api/system/organizations/{AnyOrganizationId}/deactivate" },
        { "POST", $"/api/system/organizations/{AnyOrganizationId}/reactivate" }
    };

    private static HttpRequestMessage Request(string method, string path)
    {
        HttpRequestMessage request = new(new HttpMethod(method), path);
        if (path.EndsWith("/deactivate", StringComparison.Ordinal))
        {
            request.Content = JsonContent.Create(new { reason = Reason });
        }

        return request;
    }

    private async Task<Account> SeedAccountAsync(string prefix)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: $"{prefix}-{Guid.NewGuid():N}", email: $"{prefix}-{Guid.NewGuid():N}@example.com");
    }

    private async Task<Organization> SeedOrganizationAsync(string name = "Bảo Minh")
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedOrganizationAsync(context, name: name);
    }

    private async Task<OrganizationMember> SeedMemberAsync(Guid organizationId, Guid accountId)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedOrganizationMemberAsync(context, organizationId, accountId);
    }

    private async Task<Account> SeedSystemAdministratorAsync()
    {
        Account account = await SeedAccountAsync("system-admin");
        await using AppDbContext context = CreateDbContext();
        context.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));
        await context.SaveChangesAsync();
        return account;
    }

    private HttpClient ClientFor(Account account, Guid? organizationId = null)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient client, Guid organizationId, object body) =>
        client.PostAsJsonAsync($"/api/system/organizations/{organizationId}/deactivate", body);

    private static Task<HttpResponseMessage> ReactivateAsync(HttpClient client, Guid organizationId) =>
        client.PostAsync($"/api/system/organizations/{organizationId}/reactivate", null);

    private async Task<Organization> LoadOrganizationAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.Organizations.AsNoTracking().SingleAsync(o => o.Id == organizationId);
    }

    private async Task<List<AuditLog>> LoadEventsAsync()
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs.AsNoTracking()
            .Where(log => log.Kind == AuditLogKind.Event)
            .OrderBy(log => log.Id)
            .ToListAsync();
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    // Authorization

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AllEndpoints_Anonymous_Return401(string method, string path)
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC-126, AC-131: even the owner of an organization is not a system administrator.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AllEndpoints_OwnerNotSystemAdministrator_Return403SystemAdministratorRequired(
        string method, string path)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Owner);

        HttpResponseMessage response = await client.SendAsync(Request(method, path));

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.SystemAdministratorRequired);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AllEndpoints_ApiKey_Return401(string method, string path)
    {
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "system-api-key");
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // TECH00 §5.2: the policy ignores org_id, and neither the tenant middleware nor the organization gate applies.
    [Theory]
    [InlineData("personal")]
    [InlineData("suspended")]
    [InlineData("lost")]
    public async Task AllEndpoints_SystemAdministratorWhateverOrganizationContext_AreAllowed(string context)
    {
        Account admin = await SeedSystemAdministratorAsync();
        Guid? workingOrganizationId = null;
        if (context != "personal")
        {
            Organization working = await SeedOrganizationAsync("Nơi làm việc");
            OrganizationMember membership = await SeedMemberAsync(working.Id, admin.Id);
            await using AppDbContext db = CreateDbContext();
            if (context == "suspended")
            {
                working.Deactivate(new DeactivationParams(admin.Id, false, null, DateTime.UtcNow));
                db.Organizations.Update(working);
            }
            else
            {
                membership.Deactivate();
                db.OrganizationMembers.Update(membership);
            }

            await db.SaveChangesAsync();
            workingOrganizationId = working.Id;
        }

        Organization target = await SeedOrganizationAsync("Đích");
        HttpClient client = ClientFor(admin, workingOrganizationId);

        HttpResponseMessage list = await client.GetAsync("/api/system/organizations");
        HttpResponseMessage deactivate = await DeactivateAsync(client, target.Id, new { reason = Reason });
        HttpResponseMessage reactivate = await ReactivateAsync(client, target.Id);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
    }

    // T8, AC-51, AC-52: no HTTP route grants or revokes system administrators.
    [Fact]
    public void SystemRoutes_AreExactlyTheThreeOrganizationActions()
    {
        IEnumerable<EndpointDataSource> sources = fixture.Services.GetServices<EndpointDataSource>();

        List<string> systemRoutes = sources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => new PathString("/" + endpoint.RoutePattern.RawText?.TrimStart('/'))
                .StartsWithSegments("/api/system", StringComparison.OrdinalIgnoreCase))
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText!.TrimStart('/')}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "GET api/system/organizations",
                "POST api/system/organizations/{id:guid}/deactivate",
                "POST api/system/organizations/{id:guid}/reactivate"
            ],
            systemRoutes);
    }

    // List

    // AC-124, AC-125, AC-302
    [Fact]
    public async Task List_SystemAdministrator_ReturnsEveryOrganizationWithActiveMemberCount()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization active = await SeedOrganizationAsync("an Bình");
        Organization deactivated = await SeedOrganizationAsync("Bảo Minh");
        Account member = await SeedAccountAsync("member");
        await SeedMemberAsync(active.Id, member.Id);
        HttpClient client = ClientFor(admin);
        await DeactivateAsync(client, deactivated.Id, new { reason = Reason });

        HttpResponseMessage response = await client.GetAsync("/api/system/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<SystemOrganizationDto>? page =
            await response.Content.ReadJsonAsync<PagedResult<SystemOrganizationDto>>();
        Assert.Equal(
            [new SystemOrganizationDto(active.Id, "an Bình", 1, false),
             new SystemOrganizationDto(deactivated.Id, "Bảo Minh", 0, true)],
            page!.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(50, page.PageSize);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("pageNumber=0")]
    public async Task List_PageSizeOver100_Returns400(string query)
    {
        Account admin = await SeedSystemAdministratorAsync();

        HttpResponseMessage response = await ClientFor(admin).GetAsync($"/api/system/organizations?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        CodedProblemDetails? problem = await response.Content.ReadJsonAsync<CodedProblemDetails>();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem?.Code);
    }

    // Deactivate

    [Fact]
    public async Task Deactivate_UnknownId_Returns404()
    {
        Account admin = await SeedSystemAdministratorAsync();

        HttpResponseMessage response = await DeactivateAsync(ClientFor(admin), Guid.NewGuid(), new { reason = Reason });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // AC-138, AC-139
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Deactivate_NoReason_Returns400ReasonRequiredAndOrganizationStaysActive(string? reason)
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();

        HttpResponseMessage response = await DeactivateAsync(ClientFor(admin), organization.Id, new { reason });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.DeactivationReasonRequired);
        Assert.True((await LoadOrganizationAsync(organization.Id)).Status);
        Assert.Empty(await LoadEventsAsync());
    }

    [Fact]
    public async Task Deactivate_ReasonOver500_Returns400()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();

        HttpResponseMessage response = await DeactivateAsync(
            ClientFor(admin), organization.Id, new { reason = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        CodedProblemDetails? problem = await response.Content.ReadJsonAsync<CodedProblemDetails>();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem?.Code);
        Assert.True((await LoadOrganizationAsync(organization.Id)).Status);
    }

    // AC-105, AC-183: the member's very next request sees the suspension; no cache entry outlives it.
    [Fact]
    public async Task Deactivate_MemberNextRequest_Returns403Suspended()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();
        Account member = await SeedAccountAsync("member");
        await SeedMemberAsync(organization.Id, member.Id);
        HttpClient memberClient = ClientFor(member, organization.Id);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/notifications")).StatusCode);

        HttpResponseMessage deactivate = await DeactivateAsync(ClientFor(admin), organization.Id, new { reason = Reason });
        HttpResponseMessage next = await memberClient.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        await AssertCodeAsync(next, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
    }

    // AC-368: the event lands in the organization acted on, never in the one the administrator works in.
    [Fact]
    public async Task Deactivate_AdminWorkingInOtherOrganization_EventInTargetOnly()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization working = await SeedOrganizationAsync("Cát Tường");
        await SeedMemberAsync(working.Id, admin.Id);
        Organization target = await SeedOrganizationAsync("An Bình");

        HttpResponseMessage response = await DeactivateAsync(
            ClientFor(admin, working.Id), target.Id, new { reason = Reason });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AuditLog entry = Assert.Single(await LoadEventsAsync());
        Assert.Equal(target.Id, entry.OrganizationId);
        Assert.Equal(nameof(AuditEventType.OrganizationDeactivated), entry.Action);
        Assert.Equal(nameof(ActorKind.SystemAdministrator), entry.ActorKind);
        Assert.Equal(admin.Id.ToString(), entry.UserId);
        Assert.Null(entry.NewValues);
        Assert.Null(entry.IpAddress);
        Assert.Null(entry.UserAgent);
        Organization stored = await LoadOrganizationAsync(target.Id);
        Assert.False(stored.Status);
        Assert.True(stored.DeactivatedBySystemAdministrator);
        Assert.Equal(Reason, stored.DeactivationReason);
    }

    // AC-375: an administrator who belongs to the organization acts as its member, both ways.
    [Fact]
    public async Task DeactivateThenReactivate_AdminWhoIsMember_BothEventsAccount()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();
        await SeedMemberAsync(organization.Id, admin.Id);
        HttpClient client = ClientFor(admin);

        HttpResponseMessage deactivate = await DeactivateAsync(client, organization.Id, new { reason = Reason });
        Organization afterDeactivation = await LoadOrganizationAsync(organization.Id);
        HttpResponseMessage reactivate = await ReactivateAsync(client, organization.Id);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        Assert.False(afterDeactivation.DeactivatedBySystemAdministrator);
        List<AuditLog> events = await LoadEventsAsync();
        Assert.Equal(
            [nameof(AuditEventType.OrganizationDeactivated), nameof(AuditEventType.OrganizationReactivated)],
            events.Select(entry => entry.Action));
        Assert.All(events, entry =>
        {
            Assert.Equal(organization.Id, entry.OrganizationId);
            Assert.Equal(nameof(ActorKind.Account), entry.ActorKind);
            Assert.Equal(admin.Id.ToString(), entry.UserId);
            Assert.Null(entry.NewValues);
        });
    }

    [Fact]
    public async Task Deactivate_AlreadyDeactivated_Returns204AndNoNewEvent()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();
        HttpClient client = ClientFor(admin);
        await DeactivateAsync(client, organization.Id, new { reason = Reason });

        HttpResponseMessage again = await DeactivateAsync(client, organization.Id, new { reason = "Lý do khác" });

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Single(await LoadEventsAsync());
        Assert.Equal(Reason, (await LoadOrganizationAsync(organization.Id)).DeactivationReason);
    }

    // Reactivate

    [Fact]
    public async Task Reactivate_UnknownId_Returns404()
    {
        Account admin = await SeedSystemAdministratorAsync();

        HttpResponseMessage response = await ReactivateAsync(ClientFor(admin), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_Active_Returns204AndNoEvent()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();

        HttpResponseMessage response = await ReactivateAsync(ClientFor(admin), organization.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await LoadEventsAsync());
    }

    // AC-106, AC-128: members and their roles come back untouched, and the member gets straight back in.
    [Fact]
    public async Task Reactivate_KeepsMembersAndRoles()
    {
        Account admin = await SeedSystemAdministratorAsync();
        Organization organization = await SeedOrganizationAsync();
        Account member = await SeedAccountAsync("member");
        OrganizationMember membership = await SeedMemberAsync(organization.Id, member.Id);
        HttpClient adminClient = ClientFor(admin);
        HttpClient memberClient = ClientFor(member, organization.Id);
        await DeactivateAsync(adminClient, organization.Id, new { reason = Reason });
        await AssertCodeAsync(
            await memberClient.GetAsync("/api/notifications"), HttpStatusCode.Forbidden,
            ApplicationMessages.OrganizationSuspended);

        HttpResponseMessage response = await ReactivateAsync(adminClient, organization.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/notifications")).StatusCode);
        await using AppDbContext context = CreateDbContext();
        OrganizationMember stored = await context.OrganizationMembers.AsNoTracking()
            .SingleAsync(m => m.Id == membership.Id);
        Assert.True(stored.IsActive);
        Assert.True(await context.OrganizationMemberRoles.AsNoTracking()
            .AnyAsync(role => role.OrganizationMemberId == membership.Id));
        Organization reactivated = await context.Organizations.AsNoTracking().SingleAsync(o => o.Id == organization.Id);
        Assert.True(reactivated.Status);
        Assert.Null(reactivated.DeactivatedAt);
        Assert.Null(reactivated.DeactivatedByAccountId);
        Assert.Null(reactivated.DeactivationReason);
        Assert.False(reactivated.DeactivatedBySystemAdministrator);
    }
}
