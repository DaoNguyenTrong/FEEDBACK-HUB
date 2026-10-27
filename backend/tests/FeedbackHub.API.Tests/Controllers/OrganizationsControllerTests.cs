using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.PermissionCatalog;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class OrganizationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(HttpClient Client, Account Account)> CreateAuthedClientAsync(Guid? organizationId = null)
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organizationId));
        return (client, caller);
    }

    private async Task<List<AuditLog>> LoadEventsAsync()
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs.AsNoTracking().Where(log => log.Kind == AuditLogKind.Event).ToListAsync();
    }

    private static async Task<JsonElement> AssertCodeAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Organizations_Anonymous_Returns401(string method)
    {
        HttpRequestMessage request = new(new HttpMethod(method), "/api/organizations");
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new CreateOrganizationRequest("Acme Inc", "acme-anonymous"));
        }

        HttpResponseMessage response = await fixture.CreateTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Organizations_ApiKey_Returns401(string method)
    {
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "org-api-key");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        HttpRequestMessage request = new(new HttpMethod(method), "/api/organizations");
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new CreateOrganizationRequest("Acme Inc", "acme-api-key"));
        }

        HttpResponseMessage response = await client.SendAsync(request);

        await AssertCodeAsync(response, HttpStatusCode.Unauthorized, ApplicationMessages.AuthenticatedUserRequired);
    }

    // AC-351, BR-23
    [Fact]
    public async Task Create_Unconfirmed_Returns403EmailNotConfirmed()
    {
        Account unconfirmed = Account.Create(new AccountParams(
            "Chưa xác nhận", $"unconfirmed-{Guid.NewGuid():N}", $"unconfirmed-{Guid.NewGuid():N}@example.com"));
        await using (AppDbContext context = CreateDbContext())
        {
            context.Accounts.Add(unconfirmed);
            await context.SaveChangesAsync();
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(unconfirmed));
        string slug = $"acme-{Guid.NewGuid():N}";

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Acme Inc", slug));

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.EmailNotConfirmed);
        await using AppDbContext verify = CreateDbContext();
        Assert.False(await verify.Organizations.AnyAsync(o => o.Slug == slug));
    }

    // AC-283, AC-284
    [Theory]
    [InlineData("ab")]
    [InlineData("acme inc")]
    [InlineData("-acme")]
    [InlineData("acme--inc")]
    [InlineData("tổ-chức")]
    [InlineData("a12345678901234567890123456789012345678901234567890")]
    public async Task Create_InvalidSlug_Returns400SlugInvalid(string slug)
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Acme Inc", slug));

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, DomainMessages.OrganizationSlugInvalid);
    }

    // AC-286, AC-330: the conflict never tells who holds the slug, even a suspended organization.
    [Fact]
    public async Task Create_SlugUsedBySuspendedOrganization_Returns409WithoutItsName()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        string slug = $"held-{Guid.NewGuid():N}";
        const string holderName = "Công ty Giữ Chỗ";
        await using (AppDbContext context = CreateDbContext())
        {
            Organization holder = await AuthTestHelper.SeedOrganizationAsync(context, name: holderName, slug: slug);
            holder.Deactivate(new DeactivationParams(caller.Id, false, null, DateTime.UtcNow));
            context.Organizations.Update(holder);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Another Org", slug.ToUpperInvariant()));

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.OrganizationSlugAlreadyExists);
        Assert.DoesNotContain(holderName, await response.Content.ReadAsStringAsync());
    }

    // AC-282, AC-329
    [Fact]
    public async Task Create_UppercaseSlug_CreatedLowercase()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        string slug = $"ACME-{Guid.NewGuid():N}".ToUpperInvariant();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Acme Inc", $"  {slug} "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrganizationDto? dto = await response.Content.ReadJsonAsync<OrganizationDto>();
        Assert.Equal(slug.ToLowerInvariant(), dto!.Slug);
    }

    // AC-364
    [Fact]
    public async Task Create_ThenAuditLog_HasNoEventRow()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Acme Inc", $"acme-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await LoadEventsAsync());
    }

    // AC-73
    [Fact]
    public async Task ListMine_IncludesDeactivatedOrganization()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization active;
        Organization deactivated;
        await using (AppDbContext context = CreateDbContext())
        {
            active = await AuthTestHelper.SeedOrganizationAsync(context, name: "Active");
            deactivated = await AuthTestHelper.SeedOrganizationAsync(context, name: "Deactivated");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, active.Id, caller.Id, SystemRoleKind.Member);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, deactivated.Id, caller.Id, SystemRoleKind.Owner);
            deactivated.Deactivate(new DeactivationParams(caller.Id, false, null, DateTime.UtcNow));
            context.Organizations.Update(deactivated);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<OrganizationDto>? organizations = await response.Content.ReadJsonAsync<List<OrganizationDto>>();
        Assert.Equal(2, organizations!.Count);
        Assert.True(organizations.Single(o => o.Id == active.Id).Status);
        Assert.False(organizations.Single(o => o.Id == deactivated.Id).Status);
    }

    [Fact]
    public async Task ListMine_MyRolesCarrySystemKindAsName()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Guid ownerRoleId;
        await using (AppDbContext context = CreateDbContext())
        {
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            IReadOnlyDictionary<SystemRoleKind, Role> roles = await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
            ownerRoleId = roles[SystemRoleKind.Owner].Id;
        }

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        JsonElement role = (await response.Content.ReadJsonElementAsync())[0].GetProperty("myRoles")[0];
        Assert.Equal(ownerRoleId, role.GetProperty("id").GetGuid());
        Assert.Equal("Owner", role.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.String, role.GetProperty("systemKind").ValueKind);
        Assert.Equal(nameof(SystemRoleKind.Owner), role.GetProperty("systemKind").GetString());
    }

    // QĐ-06: deactivating goes through POST api/organization/deactivate only.
    [Fact]
    public async Task DeleteOrganization_OldRoute_Returns404()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));

        HttpResponseMessage response = await client.DeleteAsync($"/api/organizations/{organization.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using AppDbContext verify = CreateDbContext();
        Assert.True((await verify.Organizations.AsNoTracking().SingleAsync(o => o.Id == organization.Id)).Status);
    }

    // FR-59, AC-135, QĐ-06: the organization comes from the JWT only; no member or role route names one.
    public static TheoryData<string, string> OldOrganizationIdRoutes => new()
    {
        { "GET", "members" },
        { "POST", "members" },
        { "PATCH", "members/{account}" },
        { "DELETE", "members/{account}" },
        { "GET", "roles" },
        { "POST", "roles" },
        { "PATCH", "roles/{role}" },
        { "DELETE", "roles/{role}" }
    };

    private sealed record OldRouteTarget(Organization Organization, Account Member, Guid MemberRoleId, Role CustomRole);

    // The caller owns the organization and the token names it: every old route would have let this caller through.
    private async Task<(HttpClient Client, OldRouteTarget Target)> CreateOwnerOfOldRouteTargetAsync()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        OldRouteTarget target;
        await using (AppDbContext context = CreateDbContext())
        {
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            Account member = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"member-{Guid.NewGuid():N}", email: $"member-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, member.Id, SystemRoleKind.Member);
            IReadOnlyDictionary<SystemRoleKind, Role> systemRoles =
                await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
            Guid memberRoleId = systemRoles[SystemRoleKind.Member].Id;
            Role customRole = await AuthTestHelper.SeedCustomRoleAsync(context, organization.Id, "Hỗ trợ", []);
            target = new OldRouteTarget(organization, member, memberRoleId, customRole);
        }

        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(caller, target.Organization.Id));
        return (client, target);
    }

    private static HttpRequestMessage OldRouteRequest(string method, string path, OldRouteTarget target)
    {
        string resolved = path
            .Replace("{account}", target.Member.Id.ToString(), StringComparison.Ordinal)
            .Replace("{role}", target.CustomRole.Id.ToString(), StringComparison.Ordinal);
        HttpRequestMessage request = new(
            new HttpMethod(method), $"/api/organizations/{target.Organization.Id}/{resolved}");
        if (method is "POST" or "PATCH")
        {
            // One body valid for each old member and role action, so only the missing route can refuse it.
            request.Content = JsonContent.Create(new
            {
                email = target.Member.Email,
                roleIds = new[] { target.MemberRoleId },
                name = "Vai trò cũ",
                permissionCodes = Array.Empty<string>()
            });
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(OldOrganizationIdRoutes))]
    public async Task OldOrganizationIdRoutes_SignedIn_Return404(string method, string path)
    {
        (HttpClient client, OldRouteTarget target) = await CreateOwnerOfOldRouteTargetAsync();

        HttpResponseMessage response = await client.SendAsync(OldRouteRequest(method, path, target));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using AppDbContext verify = CreateDbContext();
        Assert.True(await verify.OrganizationMembers.AsNoTracking().AnyAsync(m =>
            m.OrganizationId == target.Organization.Id && m.AccountId == target.Member.Id && m.IsActive));
        Role role = await verify.Roles.AsNoTracking().SingleAsync(r => r.Id == target.CustomRole.Id);
        Assert.Equal("Hỗ trợ", role.Name);
        Assert.Equal(4, await verify.Roles.AsNoTracking().CountAsync(r => r.OrganizationId == target.Organization.Id));
    }

    [Theory]
    [MemberData(nameof(OldOrganizationIdRoutes))]
    public async Task OldOrganizationIdRoutes_ApiKey_Return404(string method, string path)
    {
        (_, OldRouteTarget target) = await CreateOwnerOfOldRouteTargetAsync();
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, target.Organization.Id, "old-route-key");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.SendAsync(OldRouteRequest(method, path, target));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // AC-260, QĐ-17: an account joins only through an invitation; the direct add creates nothing and notifies no one.
    [Fact]
    public async Task AddMember_OldRoute_DoesNotCreateMembership()
    {
        (HttpClient client, OldRouteTarget target) = await CreateOwnerOfOldRouteTargetAsync();
        Account outsider;
        await using (AppDbContext context = CreateDbContext())
        {
            outsider = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"outsider-{Guid.NewGuid():N}", email: $"outsider-{Guid.NewGuid():N}@example.com");
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{target.Organization.Id}/members",
            new { email = outsider.Email, roleIds = new[] { target.MemberRoleId } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using AppDbContext verify = CreateDbContext();
        Assert.False(await verify.OrganizationMembers.AsNoTracking().AnyAsync(m => m.AccountId == outsider.Id));
        Assert.False(await verify.Notifications.AsNoTracking().AnyAsync(n => n.AccountId == outsider.Id));
    }

    [Fact]
    public async Task Create_Valid_Returns201WithCallerAsOwner()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        CreateOrganizationRequest request = new("Acme Inc", $"acme-{Guid.NewGuid():N}");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/organizations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrganizationDto? dto = await response.Content.ReadJsonAsync<OrganizationDto>();
        Assert.Equal(request.Name, dto!.Name);
        Assert.Equal(["Owner"], dto.MyRoleNames);
        Assert.Equal(Permissions.All.OrderBy(c => c), dto.MyPermissionCodes.OrderBy(c => c));
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        string slug = $"dup-slug-{Guid.NewGuid():N}";
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedOrganizationAsync(context, slug: slug);
        }
        CreateOrganizationRequest request = new("Another Org", slug);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/organizations", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOrganizationsCallerBelongsTo()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization mine;
        await using (AppDbContext context = CreateDbContext())
        {
            mine = await AuthTestHelper.SeedOrganizationAsync(context, name: "Mine");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, mine.Id, caller.Id, SystemRoleKind.Admin);

            Organization notMine = await AuthTestHelper.SeedOrganizationAsync(context, name: "NotMine");
            Account otherOwner = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-owner-{Guid.NewGuid():N}", email: $"other-owner-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, notMine.Id, otherOwner.Id, SystemRoleKind.Owner);
        }

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<OrganizationDto>? organizations = await response.Content.ReadJsonAsync<List<OrganizationDto>>();
        OrganizationDto only = Assert.Single(organizations!);
        Assert.Equal(mine.Id, only.Id);
        Assert.Equal(["Admin"], only.MyRoleNames);
        Assert.Equal(new[] { Permissions.OrganizationMembersManage }, only.MyPermissionCodes);
    }

    [Fact]
    public async Task GetPermissions_ReturnsFixedCatalog()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<PermissionDto>? permissions = await response.Content.ReadJsonAsync<List<PermissionDto>>();
        Assert.Equal(
            Permissions.All.OrderBy(c => c),
            permissions!.Select(p => p.Code).OrderBy(c => c));
    }
}
