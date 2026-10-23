using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CurrentOrganizationControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/organization";
    private const string DeactivatePath = "/api/organization/deactivate";
    private const string Reason = "Hết hợp đồng";
    private const int ParallelRequests = 8;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", Path },
        { "PATCH", Path },
        { "POST", DeactivatePath }
    };

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

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

    private async Task SeedMemberAsync(Guid organizationId, Guid accountId, SystemRoleKind role)
    {
        await using AppDbContext context = CreateDbContext();
        await AuthTestHelper.SeedOrganizationMemberAsync(context, organizationId, accountId, role);
    }

    private async Task MakeSystemAdministratorAsync(Guid accountId)
    {
        await using AppDbContext context = CreateDbContext();
        context.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(accountId)));
        await context.SaveChangesAsync();
    }

    /// <summary>A caller working in a fresh organization with the given role.</summary>
    private async Task<(HttpClient Client, Account Caller, Organization Organization)> CreateCallerAsync(
        SystemRoleKind role, string organizationName = "Bảo Minh")
    {
        Account caller = await SeedAccountAsync("caller");
        Organization organization = await SeedOrganizationAsync(organizationName);
        await SeedMemberAsync(organization.Id, caller.Id, role);
        return (ClientFor(caller, organization.Id), caller, organization);
    }

    private HttpClient ClientFor(Account account, Guid? organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private static HttpRequestMessage Request(string method, string path)
    {
        HttpRequestMessage request = new(new HttpMethod(method), path);
        if (method != "GET")
        {
            request.Content = JsonContent.Create(new { name = "Tên mới", reason = Reason });
        }

        return request;
    }

    private static Task<HttpResponseMessage> RenameAsync(HttpClient client, object body) =>
        client.PatchAsJsonAsync(Path, body);

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(DeactivatePath, body);

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

    private async Task<int> CountOrganizationChangeRowsAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs.AsNoTracking().CountAsync(log =>
            log.Kind == AuditLogKind.Change
            && log.EntityName == nameof(Organization)
            && log.EntityId == organizationId.ToString()
            && log.Action == nameof(EntityState.Modified));
    }

    private static async Task<JsonElement> AssertCodeAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }

    // Authorization

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CurrentOrganization_Anonymous_Returns401(string method, string path)
    {
        HttpResponseMessage response = await fixture.CreateTestClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC-299
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task CurrentOrganization_PersonalContext_Returns403AccessDenied(string method, string path)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, personalContext: true);

        HttpResponseMessage response = await client.SendAsync(Request(method, path));

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // AC-301
    [Fact]
    public async Task Get_SuspendedMember_Returns403SuspendedWithName()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync(Path);

        JsonElement body = await AssertCodeAsync(
            response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
        Assert.Equal(AuthTestHelper.GateOrganizationName, body.GetProperty("organizationName").GetString());
    }

    // F4: a policy checked in the JWT's organization on a route naming another organization would let a manager
    // of one act on the other; organizations.manage in particular sits on no route with an id.
    [Fact]
    public void ActiveOrganizationScopedPolicies_AreNeverOnARouteNamingAnOrganization()
    {
        List<RouteEndpoint> endpoints = fixture.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        List<string> offending = endpoints
            .Where(endpoint => UsesPolicy(endpoint, Permissions.ActiveOrganizationScoped.Contains)
                && (endpoint.RoutePattern.RawText ?? string.Empty)
                    .StartsWith("api/organizations/{", StringComparison.Ordinal))
            .Concat(endpoints.Where(endpoint => UsesPolicy(endpoint, policy => policy == Permissions.OrganizationManage)
                && endpoint.RoutePattern.Parameters.Count > 0))
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.Empty(offending);
        Assert.Contains(Permissions.OrganizationManage, Permissions.ActiveOrganizationScoped);
    }

    private static bool UsesPolicy(RouteEndpoint endpoint, Func<string, bool> matches) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(data => data.Policy is not null && matches(data.Policy));

    // Get

    [Fact]
    public async Task Get_Member_Returns200WithWorkingOrganization()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CurrentOrganizationDto? dto = await response.Content.ReadJsonAsync<CurrentOrganizationDto>();
        Assert.Equal(new CurrentOrganizationDto(organization.Id, "Bảo Minh", organization.Slug, true), dto);
    }

    // Rename

    // AC-186
    [Fact]
    public async Task Rename_WithoutOrganizationsManage_Returns403AndNameUnchanged()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Admin);

        HttpResponseMessage response = await RenameAsync(client, new { name = "Tên mới" });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Equal("Bảo Minh", (await LoadOrganizationAsync(organization.Id)).Name);
        Assert.Empty(await LoadEventsAsync());
    }

    // The permission is checked before the name is compared.
    [Fact]
    public async Task Rename_SameNameWithoutOrganizationsManage_Returns403()
    {
        (HttpClient client, _, _) = await CreateCallerAsync(SystemRoleKind.Member);

        HttpResponseMessage response = await RenameAsync(client, new { name = "Bảo Minh" });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // AC-185, AC-225, AC-367
    [Fact]
    public async Task Rename_NewName_Returns200AndListMineShowsIt_SlugUnchanged()
    {
        (HttpClient client, Account caller, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name = "  Bảo Minh Group " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CurrentOrganizationDto? dto = await response.Content.ReadJsonAsync<CurrentOrganizationDto>();
        Assert.Equal(new CurrentOrganizationDto(organization.Id, "Bảo Minh Group", organization.Slug, true), dto);
        List<OrganizationDto>? mine = await (await client.GetAsync("/api/organizations"))
            .Content.ReadJsonAsync<List<OrganizationDto>>();
        OrganizationDto listed = Assert.Single(mine!);
        Assert.Equal("Bảo Minh Group", listed.Name);
        Assert.Equal(organization.Slug, listed.Slug);
        AuditLog entry = Assert.Single(await LoadEventsAsync());
        Assert.Equal(nameof(AuditEventType.OrganizationRenamed), entry.Action);
        Assert.Equal(organization.Id, entry.OrganizationId);
        Assert.Equal(nameof(ActorKind.Account), entry.ActorKind);
        Assert.Equal(caller.Id.ToString(), entry.UserId);
        using JsonDocument parameters = JsonDocument.Parse(entry.NewValues!);
        Assert.Equal("Bảo Minh", parameters.RootElement.GetProperty("nameBefore").GetString());
        Assert.Equal("Bảo Minh Group", parameters.RootElement.GetProperty("nameAfter").GetString());
    }

    // AC-187
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rename_BlankName_Returns400OrganizationNameRequired(string name)
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, DomainMessages.OrganizationNameRequired);
        Assert.Equal("Bảo Minh", (await LoadOrganizationAsync(organization.Id)).Name);
    }

    [Fact]
    public async Task Rename_NameOver200_Returns400()
    {
        (HttpClient client, _, _) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name = new string('x', 201) });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.ValidationFailed);
    }

    [Fact]
    public async Task Rename_SameNameAfterTrim_Returns200WithoutSaveOrAudit()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name = " Bảo Minh  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bảo Minh", (await response.Content.ReadJsonAsync<CurrentOrganizationDto>())!.Name);
        Assert.Empty(await LoadEventsAsync());
        Assert.Equal(0, await CountOrganizationChangeRowsAsync(organization.Id));
    }

    [Fact]
    public async Task Rename_CaseOnlyChange_Returns200AndLogsRenamed()
    {
        (HttpClient client, _, _) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name = "bảo minh" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditLog entry = Assert.Single(await LoadEventsAsync());
        using JsonDocument parameters = JsonDocument.Parse(entry.NewValues!);
        Assert.Equal("Bảo Minh", parameters.RootElement.GetProperty("nameBefore").GetString());
        Assert.Equal("bảo minh", parameters.RootElement.GetProperty("nameAfter").GetString());
    }

    // AC-226: the slug is not part of the request; a body with only a slug has no name.
    [Fact]
    public async Task Rename_BodyWithSlugOnly_Returns400OrganizationNameRequired()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { slug = "slug-moi" });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, DomainMessages.OrganizationNameRequired);
        Assert.Equal(organization.Slug, (await LoadOrganizationAsync(organization.Id)).Slug);
    }

    // AC-226
    [Fact]
    public async Task Rename_BodyWithNameAndSlug_RenamesAndSlugUnchanged()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await RenameAsync(client, new { name = "Tên mới", slug = "slug-moi" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Organization stored = await LoadOrganizationAsync(organization.Id);
        Assert.Equal("Tên mới", stored.Name);
        Assert.Equal(organization.Slug, stored.Slug);
    }

    // Deactivate

    [Fact]
    public async Task Deactivate_WithoutOrganizationsManage_Returns403AndStillActive()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Admin);

        HttpResponseMessage response = await DeactivateAsync(client, new { reason = Reason });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.True((await LoadOrganizationAsync(organization.Id)).Status);
    }

    // AC-146, AC-183: the very next request of any member sees the suspension.
    [Fact]
    public async Task Deactivate_Manager_Returns204AndNextRequest403Suspended()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);
        Account member = await SeedAccountAsync("member");
        await SeedMemberAsync(organization.Id, member.Id, SystemRoleKind.Member);
        HttpClient memberClient = ClientFor(member, organization.Id);
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync(Path)).StatusCode);

        HttpResponseMessage response = await DeactivateAsync(client, new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertCodeAsync(
            await client.GetAsync(Path), HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
        await AssertCodeAsync(
            await memberClient.GetAsync(Path), HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
    }

    // AC-147, BR-54
    [Fact]
    public async Task Deactivate_SystemAdministratorMemberWithoutReason_Returns400StillActive()
    {
        (HttpClient client, Account caller, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);
        await MakeSystemAdministratorAsync(caller.Id);

        HttpResponseMessage response = await DeactivateAsync(client, new { reason = "  " });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.DeactivationReasonRequired);
        Assert.True((await LoadOrganizationAsync(organization.Id)).Status);
        Assert.Empty(await LoadEventsAsync());
    }

    [Fact]
    public async Task Deactivate_ReasonOver500_Returns400()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await DeactivateAsync(client, new { reason = new string('x', 501) });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.ValidationFailed);
        Assert.True((await LoadOrganizationAsync(organization.Id)).Status);
    }

    // AC-367, AC-369: the event names the member who acted, and the reason stays on the organization only.
    [Fact]
    public async Task Deactivate_EventHasAccountActorAndNoReason()
    {
        (HttpClient client, Account caller, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);
        await MakeSystemAdministratorAsync(caller.Id);

        HttpResponseMessage response = await DeactivateAsync(client, new { reason = Reason });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AuditLog entry = Assert.Single(await LoadEventsAsync());
        Assert.Equal(nameof(AuditEventType.OrganizationDeactivated), entry.Action);
        Assert.Equal(organization.Id, entry.OrganizationId);
        Assert.Equal(nameof(ActorKind.Account), entry.ActorKind);
        Assert.Equal(caller.Id.ToString(), entry.UserId);
        Assert.Null(entry.NewValues);
        Organization stored = await LoadOrganizationAsync(organization.Id);
        Assert.False(stored.Status);
        Assert.False(stored.DeactivatedBySystemAdministrator);
        Assert.Equal(Reason, stored.DeactivationReason);
    }

    // F4: a manager of one organization cannot reach another one, even one they belong to.
    [Fact]
    public async Task Deactivate_ManagerOfAnotherOrganization_OnlyTokenOrganizationIsDeactivated()
    {
        Account caller = await SeedAccountAsync("caller");
        Organization managed = await SeedOrganizationAsync("Managed");
        Organization other = await SeedOrganizationAsync("Other");
        await SeedMemberAsync(managed.Id, caller.Id, SystemRoleKind.Owner);
        await SeedMemberAsync(other.Id, caller.Id, SystemRoleKind.Member);

        HttpResponseMessage inOther = await DeactivateAsync(ClientFor(caller, other.Id), new { reason = Reason });
        HttpResponseMessage inManaged = await DeactivateAsync(ClientFor(caller, managed.Id), new { reason = Reason });

        await AssertCodeAsync(inOther, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Equal(HttpStatusCode.NoContent, inManaged.StatusCode);
        Assert.True((await LoadOrganizationAsync(other.Id)).Status);
        Assert.False((await LoadOrganizationAsync(managed.Id)).Status);
    }

    // TECH00 §3.7, AC-367: requests that pass the gate together still deactivate once and record one event.
    [Fact]
    public async Task Deactivate_ParallelRequests_RecordOneEvent()
    {
        (HttpClient client, _, Organization organization) = await CreateCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, ParallelRequests)
            .Select(_ => DeactivateAsync(client, new { reason = Reason })));

        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Forbidden));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(await LoadEventsAsync());
        Assert.False((await LoadOrganizationAsync(organization.Id)).Status);
    }
}
