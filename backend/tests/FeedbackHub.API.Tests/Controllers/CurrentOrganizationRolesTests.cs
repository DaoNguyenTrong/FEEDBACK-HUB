using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CurrentOrganizationRolesTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string OptionsPath = "/api/organization/role-options";
    private const string RolesPath = "/api/organization/roles";

    public static TheoryData<string> Paths => new() { OptionsPath, RolesPath };

    public static TheoryData<string> Writes => new() { "create", "update", "delete" };

    private static readonly string EntityNotFoundCode = new NotFoundException(nameof(Role), Guid.Empty).Message;

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

    // Writes: helpers

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private sealed record Caller(Organization Organization, Account Account, OrganizationMember Member, HttpClient Client);

    /// <summary>A caller in a fresh organization: an Owner, or a Member who also holds a custom role with these codes.</summary>
    private async Task<Caller> SeedCallerAsync(bool owner = true, params string[] customCodes)
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"roles-{Guid.NewGuid():N}";
        Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: handle, email: $"{handle}@example.com");
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: "Bảo Minh");
        OrganizationMember member = await AuthTestHelper.SeedOrganizationMemberAsync(
            context, organization.Id, account.Id, owner ? SystemRoleKind.Owner : SystemRoleKind.Member);
        if (!owner)
        {
            Role manager = await AuthTestHelper.SeedCustomRoleAsync(context, organization.Id, "Quản lý vai trò", customCodes);
            await AuthTestHelper.AssignRoleAsync(context, member.Id, manager.Id);
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));
        return new Caller(organization, account, member, client);
    }

    private async Task<Role> SeedRoleAsync(Guid organizationId, string name, params string[] codes)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedCustomRoleAsync(context, organizationId, name, codes);
    }

    private async Task<OrganizationMember> SeedHolderAsync(Guid organizationId, Role role, bool active = true)
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"holder-{Guid.NewGuid():N}";
        Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: handle, email: $"{handle}@example.com");
        OrganizationMember member = await AuthTestHelper.SeedOrganizationMemberAsync(
            context, organizationId, account.Id, SystemRoleKind.Member);
        await AuthTestHelper.AssignRoleAsync(context, member.Id, role.Id);
        if (!active)
        {
            member.Deactivate();
            context.OrganizationMembers.Update(member);
            await context.SaveChangesAsync();
        }

        return member;
    }

    private async Task<Role?> FindRoleAsync(Guid roleId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.Roles.SingleOrDefaultAsync(role => role.Id == roleId);
    }

    private async Task<List<string>> CodesOfAsync(Guid roleId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.RolePermissions
            .Where(link => link.RoleId == roleId)
            .Select(link => link.PermissionCode)
            .OrderBy(code => code)
            .ToListAsync();
    }

    private async Task<List<AuditLog>> EventsAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs
            .Where(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event)
            .ToListAsync();
    }

    private static Task<HttpResponseMessage> CreateRoleAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(RolesPath, body);

    private static Task<HttpResponseMessage> UpdateRoleAsync(HttpClient client, Guid roleId, object body) =>
        client.PutAsJsonAsync($"{RolesPath}/{roleId}", body);

    private static Task<HttpResponseMessage> DeleteRoleAsync(HttpClient client, Guid roleId) =>
        client.DeleteAsync($"{RolesPath}/{roleId}");

    private static Task<HttpResponseMessage> SendWriteAsync(HttpClient client, string write, Guid roleId) =>
        write switch
        {
            "create" => CreateRoleAsync(client, new { name = "Hỗ trợ", permissionCodes = Array.Empty<string>() }),
            "update" => UpdateRoleAsync(client, roleId, new { name = "Hỗ trợ", permissionCodes = Array.Empty<string>() }),
            _ => DeleteRoleAsync(client, roleId)
        };

    // Writes: authorization

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task RoleWrites_Anonymous_Return401(string write)
    {
        HttpResponseMessage response = await SendWriteAsync(fixture.CreateTestClient(), write, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // BR-77
    [Theory]
    [MemberData(nameof(Writes))]
    public async Task RoleWrites_WithoutRolesManage_Return403(string write)
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationMembersManage);
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester");

        HttpResponseMessage response = await SendWriteAsync(caller.Client, write, tester.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Equal("Tester", (await FindRoleAsync(tester.Id))?.Name);
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // The token and the permission cache still say "manage"; the service re-reads it after the lock.
    [Fact]
    public async Task CreateRole_CallerWhoseRolesManageWasRemovedInDatabase_Returns403AndCreatesNothing()
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationRolesManage);
        Assert.Equal(HttpStatusCode.OK, (await caller.Client.GetAsync(RolesPath)).StatusCode);
        await using (AppDbContext context = CreateDbContext())
        {
            await context.RolePermissions
                .Where(permission => permission.PermissionCode == Permissions.OrganizationRolesManage
                    && context.Roles.Any(role => role.Id == permission.RoleId
                        && role.OrganizationId == caller.Organization.Id))
                .ExecuteDeleteAsync();
        }

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name = "Hỗ trợ", permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        await using AppDbContext reader = CreateDbContext();
        Assert.False(await reader.Roles.AnyAsync(role => role.OrganizationId == caller.Organization.Id
            && role.Name == "Hỗ trợ"));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // An API key never stands in an organization (TECH00 §5.2).
    [Theory]
    [MemberData(nameof(Writes))]
    public async Task RoleWrites_ApiKey_Return403AccessDenied(string write)
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester");
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, caller.Organization.Id, $"roles-{write}");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await SendWriteAsync(client, write, tester.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Equal("Tester", (await FindRoleAsync(tester.Id))?.Name);
    }

    // CreateRole

    [Fact]
    public async Task CreateRole_Owner_Returns201WithRoleDtoAndAuditRow()
    {
        Caller caller = await SeedCallerAsync();

        HttpResponseMessage response = await CreateRoleAsync(caller.Client, new
        {
            name = "  Hỗ trợ ",
            permissionCodes = new[] { Permissions.FilesManage, Permissions.FeedbackHubAdmin }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Guid roleId = body.GetProperty("id").GetGuid();
        Assert.Equal("Hỗ trợ", body.GetProperty("name").GetString());
        Assert.Equal(0, body.GetProperty("holderCount").GetInt32());
        Assert.False(body.GetProperty("isSystem").GetBoolean());
        Assert.True(body.GetProperty("canEdit").GetBoolean());
        Assert.True(body.GetProperty("canDelete").GetBoolean());
        Assert.Equal(
            new[] { Permissions.FeedbackHubAdmin, Permissions.FilesManage }.Order(),
            body.GetProperty("permissionCodes").EnumerateArray().Select(code => code.GetString()).Order());

        Assert.Equal("Hỗ trợ", (await FindRoleAsync(roleId))?.Name);
        Assert.Equal(new[] { Permissions.FeedbackHubAdmin, Permissions.FilesManage }.Order(), await CodesOfAsync(roleId));
        AuditLog entry = Assert.Single(await EventsAsync(caller.Organization.Id));
        Assert.Equal(nameof(AuditEventType.RoleCreated), entry.Action);
        Assert.Equal(caller.Account.Id.ToString(), entry.UserId);
    }

    // AC-287, AC-288
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("101")]
    public async Task CreateRole_BlankMissingOr101CharName_Returns400OnName(string? name)
    {
        Caller caller = await SeedCallerAsync();
        string? sent = name == "101" ? new string('a', 101) : name;

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name = sent, permissionCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement problem = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "Name", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // AC-289: 100 characters is the limit, counted in characters, not bytes.
    [Fact]
    public async Task CreateRole_100CharName_Returns201()
    {
        Caller caller = await SeedCallerAsync();
        string name = new('ư', 100);

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name, permissionCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(name, (await response.Content.ReadJsonElementAsync()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateRole_MissingPermissionCodes_Returns400()
    {
        Caller caller = await SeedCallerAsync();

        HttpResponseMessage response = await CreateRoleAsync(caller.Client, new { name = "Hỗ trợ" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement problem = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "PermissionCodes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateRole_UnknownPermissionCode_Returns400()
    {
        Caller caller = await SeedCallerAsync();

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name = "Hỗ trợ", permissionCodes = new[] { "feedback.delete" } });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.UnknownPermissionCode);
    }

    // AC-156
    [Fact]
    public async Task CreateRole_PermissionCallerLacks_Returns403AndCreatesNothing()
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationRolesManage);

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name = "Trưởng IT", permissionCodes = new[] { Permissions.FeedbackHubAdmin } });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.InsufficientRolePermissions);
        await using AppDbContext context = CreateDbContext();
        Assert.False(await context.Roles.AnyAsync(role => role.OrganizationId == caller.Organization.Id
            && role.Name == "Trưởng IT"));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // AC-290, against the real unique-index expression, non-ASCII case included.
    [Theory]
    [InlineData("Tester", "tester")]
    [InlineData("Ước lượng", "ƯỚC LƯỢNG")]
    public async Task CreateRole_NameDiffersOnlyByCase_Returns409(string existing, string name)
    {
        Caller caller = await SeedCallerAsync();
        await SeedRoleAsync(caller.Organization.Id, existing);

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name, permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.OrganizationRoleNameAlreadyExists);
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // AC-291
    [Theory]
    [InlineData("Owner")]
    [InlineData("chủ sở hữu")]
    public async Task CreateRole_ReservedName_Returns409(string name)
    {
        Caller caller = await SeedCallerAsync();

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name, permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.RoleNameReservedForSystemRole);
    }

    // AC-292
    [Fact]
    public async Task CreateRole_SameNameInOtherOrganization_Returns201()
    {
        Caller caller = await SeedCallerAsync();
        Caller other = await SeedCallerAsync();
        await SeedRoleAsync(other.Organization.Id, "Tester");

        HttpResponseMessage response = await CreateRoleAsync(
            caller.Client, new { name = "Tester", permissionCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // UpdateRole

    // BR-45: a role of another organization answers like one that does not exist.
    [Fact]
    public async Task UpdateRole_RoleOfOtherOrganization_Returns404()
    {
        Caller caller = await SeedCallerAsync();
        Caller other = await SeedCallerAsync();
        Role foreign = await SeedRoleAsync(other.Organization.Id, "Tester");

        HttpResponseMessage response = await UpdateRoleAsync(
            caller.Client, foreign.Id, new { name = "Kiểm thử", permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.NotFound, EntityNotFoundCode);
        Assert.Equal("Tester", (await FindRoleAsync(foreign.Id))?.Name);
        Assert.Empty(await EventsAsync(other.Organization.Id));
    }

    // AC-55
    [Fact]
    public async Task UpdateRole_SystemRole_Returns403Immutable()
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationRolesManage,
            Permissions.OrganizationMembersManage);
        await using AppDbContext context = CreateDbContext();
        Role ownerRole = (await AuthTestHelper.SeedSystemRolesAsync(context, caller.Organization.Id))[SystemRoleKind.Owner];

        HttpResponseMessage response = await UpdateRoleAsync(
            caller.Client, ownerRole.Id, new { name = "Owner", permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSystemRoleImmutable);
    }

    // AC-295
    [Fact]
    public async Task UpdateRole_RenameRoleWithPermissionCallerLacks_Returns403()
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationRolesManage);
        Role lead = await SeedRoleAsync(caller.Organization.Id, "Trưởng IT", Permissions.FeedbackHubAdmin);

        HttpResponseMessage response = await UpdateRoleAsync(caller.Client, lead.Id, new
        {
            name = "Trưởng nhóm IT",
            permissionCodes = new[] { Permissions.FeedbackHubAdmin }
        });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.InsufficientRolePermissions);
        Assert.Equal("Trưởng IT", (await FindRoleAsync(lead.Id))?.Name);
    }

    // AC-293 (role part), AC-297
    [Fact]
    public async Task UpdateRole_OwnerRenames_KeepsHoldersAndPermissions()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester", Permissions.FilesManage);
        OrganizationMember holder = await SeedHolderAsync(caller.Organization.Id, tester);

        HttpResponseMessage response = await UpdateRoleAsync(
            caller.Client, tester.Id, new { name = "Kiểm thử", permissionCodes = new[] { Permissions.FilesManage } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal("Kiểm thử", body.GetProperty("name").GetString());
        Assert.Equal(1, body.GetProperty("holderCount").GetInt32());
        Assert.Equal("Kiểm thử", (await FindRoleAsync(tester.Id))?.Name);
        Assert.Equal([Permissions.FilesManage], await CodesOfAsync(tester.Id));
        await using AppDbContext context = CreateDbContext();
        Assert.True(await context.OrganizationMemberRoles.AnyAsync(
            link => link.OrganizationMemberId == holder.Id && link.RoleId == tester.Id));
        AuditLog entry = Assert.Single(await EventsAsync(caller.Organization.Id));
        Assert.Equal(nameof(AuditEventType.RoleUpdated), entry.Action);
    }

    // AC-326
    [Fact]
    public async Task UpdateRole_RenameToOtherRoleUppercase_Returns409()
    {
        Caller caller = await SeedCallerAsync();
        await SeedRoleAsync(caller.Organization.Id, "Tester");
        Role review = await SeedRoleAsync(caller.Organization.Id, "Kiểm thử");

        HttpResponseMessage response = await UpdateRoleAsync(
            caller.Client, review.Id, new { name = "TESTER", permissionCodes = Array.Empty<string>() });

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.OrganizationRoleNameAlreadyExists);
        Assert.Equal("Kiểm thử", (await FindRoleAsync(review.Id))?.Name);
    }

    [Fact]
    public async Task UpdateRole_NoChange_Returns200WithCurrentRoleDtoAndNoNewAuditRow()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester", Permissions.FilesManage);

        HttpResponseMessage response = await UpdateRoleAsync(caller.Client, tester.Id, new
        {
            name = "Tester ",
            permissionCodes = new[] { Permissions.FilesManage, Permissions.FilesManage }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(tester.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Tester", body.GetProperty("name").GetString());
        Assert.Equal(
            [Permissions.FilesManage],
            body.GetProperty("permissionCodes").EnumerateArray().Select(code => code.GetString()));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // The route answers PUT and DELETE only; the old PATCH is gone.
    [Fact]
    public async Task UpdateRole_OldPatchVerb_Returns405()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester");

        HttpResponseMessage response = await caller.Client.PatchAsJsonAsync(
            $"{RolesPath}/{tester.Id}", new { name = "Kiểm thử", permissionCodes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("Tester", (await FindRoleAsync(tester.Id))?.Name);
    }

    // DeleteRole

    // AC-247: the role goes, its permission rows with it.
    [Fact]
    public async Task DeleteRole_NoHolders_Returns204AndRemovesRole()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester", Permissions.FilesManage);

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, tester.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await FindRoleAsync(tester.Id));
        Assert.Empty(await CodesOfAsync(tester.Id));
        AuditLog entry = Assert.Single(await EventsAsync(caller.Organization.Id));
        Assert.Equal(nameof(AuditEventType.RoleDeleted), entry.Action);
    }

    // BR-45: a role of another organization answers like one that does not exist, and stays.
    [Fact]
    public async Task DeleteRole_RoleOfOtherOrganization_Returns404AndRoleRemains()
    {
        Caller caller = await SeedCallerAsync();
        Caller other = await SeedCallerAsync();
        Role foreign = await SeedRoleAsync(other.Organization.Id, "Tester");

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, foreign.Id);

        await AssertCodeAsync(response, HttpStatusCode.NotFound, EntityNotFoundCode);
        Assert.NotNull(await FindRoleAsync(foreign.Id));
        Assert.Empty(await EventsAsync(other.Organization.Id));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // AC-252
    [Fact]
    public async Task DeleteRole_OnlyInactiveHolder_Returns204()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester");
        await SeedHolderAsync(caller.Organization.Id, tester, active: false);

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, tester.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await FindRoleAsync(tester.Id));
    }

    // AC-248, AC-251, AC-256
    [Fact]
    public async Task DeleteRole_ActiveHolder_Returns409AndKeepsRole()
    {
        Caller caller = await SeedCallerAsync();
        Role tester = await SeedRoleAsync(caller.Organization.Id, "Tester", Permissions.FilesManage);
        await SeedHolderAsync(caller.Organization.Id, tester);

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, tester.Id);

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.RoleHasHolders);
        Assert.NotNull(await FindRoleAsync(tester.Id));
        Assert.Equal([Permissions.FilesManage], await CodesOfAsync(tester.Id));
        Assert.Empty(await EventsAsync(caller.Organization.Id));
    }

    // AC-257: the permission reason comes before the holder reason.
    [Fact]
    public async Task DeleteRole_CallerLacksPermissionOfRoleWithHolders_Returns403()
    {
        Caller caller = await SeedCallerAsync(owner: false, Permissions.OrganizationRolesManage);
        Role lead = await SeedRoleAsync(caller.Organization.Id, "Trưởng IT", Permissions.FeedbackHubAdmin);
        await SeedHolderAsync(caller.Organization.Id, lead);

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, lead.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.InsufficientRolePermissions);
        Assert.NotNull(await FindRoleAsync(lead.Id));
    }

    [Fact]
    public async Task DeleteRole_SystemRole_Returns403Immutable()
    {
        Caller caller = await SeedCallerAsync();
        await using AppDbContext context = CreateDbContext();
        Role member = (await AuthTestHelper.SeedSystemRolesAsync(context, caller.Organization.Id))[SystemRoleKind.Member];

        HttpResponseMessage response = await DeleteRoleAsync(caller.Client, member.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSystemRoleImmutable);
    }
}
