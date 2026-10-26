using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

// Every catalogue operation (TECH00 §3.7) is driven through its own endpoint and the result is read back through
// API-33 by a member who holds nothing but auditlogs.view, so these tests prove the entry reaches the right log.
[Collection(nameof(ApiCollection))]
public sealed class AuditLogEndToEndTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string LogPath = "/api/organization/audit-log";
    private const string OrganizationPath = "/api/organization";
    private const string OrganizationName = "Bảo Minh";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private sealed record Scene(
        Organization Organization,
        Account Owner,
        Account Viewer,
        IReadOnlyDictionary<SystemRoleKind, Role> Roles);

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private HttpClient ClientFor(Account account, Guid? organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private async Task<Account> SeedAccountAsync(string name)
    {
        string handle = $"e2e-{Guid.NewGuid():N}";
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: handle, email: $"{handle}@example.com", name: name);
    }

    // An organization, the Owner who acts, and a viewer whose custom role holds only auditlogs.view.
    private async Task<Scene> SeedSceneAsync(string organizationName = OrganizationName)
    {
        Account owner = await SeedAccountAsync("Chủ sở hữu");
        Account viewer = await SeedAccountAsync("Người xem");
        await using AppDbContext context = CreateDbContext();
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: organizationName);
        await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, owner.Id, SystemRoleKind.Owner);
        OrganizationMember viewerMember = await AuthTestHelper.SeedOrganizationMemberAsync(
            context, organization.Id, viewer.Id, SystemRoleKind.Member);
        Role viewerRole = await AuthTestHelper.SeedCustomRoleAsync(
            context, organization.Id, "Người xem nhật ký", [Permissions.AuditLogsView]);
        await AuthTestHelper.AssignRoleAsync(context, viewerMember.Id, viewerRole.Id);
        return new Scene(
            organization, owner, viewer, await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id));
    }

    private async Task<Account> SeedMemberAsync(Scene scene, SystemRoleKind role, string name)
    {
        Account account = await SeedAccountAsync(name);
        await using AppDbContext context = CreateDbContext();
        await AuthTestHelper.SeedOrganizationMemberAsync(context, scene.Organization.Id, account.Id, role);
        return account;
    }

    private async Task<Account> SeedSystemAdministratorAsync(Guid? memberOfOrganizationId = null)
    {
        Account account = await SeedAccountAsync("Quản trị hệ thống");
        await using AppDbContext context = CreateDbContext();
        context.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));
        await context.SaveChangesAsync();
        if (memberOfOrganizationId is { } organizationId)
        {
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organizationId, account.Id, SystemRoleKind.Member);
        }

        return account;
    }

    private async Task DeactivateInDatabaseAsync(Scene scene)
    {
        await using AppDbContext context = CreateDbContext();
        Organization stored = await context.Organizations.SingleAsync(o => o.Id == scene.Organization.Id);
        stored.Deactivate(new DeactivationParams(scene.Owner.Id, false, null, DateTime.UtcNow));
        context.Organizations.Update(stored);
        await context.SaveChangesAsync();
    }

    private async Task<OrganizationInvitation> SeedInvitationAsync(
        Scene scene, string email, string rawToken, DateTime? issuedAt = null)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedInvitationAsync(
            context, scene.Organization.Id, email, scene.Roles[SystemRoleKind.Member].Id, rawToken, issuedAt);
    }

    private static Task<HttpResponseMessage> ReactivateAsync(HttpClient client, Guid organizationId) =>
        client.PostAsync($"/api/system/organizations/{organizationId}/reactivate", null);

    // The newest entries first, as the viewer reads them through API-33.
    private async Task<JsonElement[]> ReadEntriesAsync(Scene scene)
    {
        HttpResponseMessage response = await ClientFor(scene.Viewer, scene.Organization.Id).GetAsync(LogPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement page = await response.Content.ReadJsonElementAsync();
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private async Task<JsonElement> ReadNewestAsync(Scene scene) => (await ReadEntriesAsync(scene))[0];

    // A scene starts with an empty log, so the operation under test must have written exactly this one entry.
    private async Task<JsonElement> ReadOnlyEntryAsync(Scene scene)
    {
        JsonElement[] entries = await ReadEntriesAsync(scene);
        return Assert.Single(entries);
    }

    private static void AssertEntry(JsonElement entry, string type, string actorKind, string? actorName)
    {
        Assert.Equal(type, entry.GetProperty("type").GetString());
        Assert.Equal(actorKind, entry.GetProperty("actorKind").GetString());
        if (actorName is null)
        {
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("actorName").ValueKind);
        }
        else
        {
            Assert.Equal(actorName, entry.GetProperty("actorName").GetString());
        }
    }

    private static void AssertParameterKeys(JsonElement entry, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            entry.GetProperty("parameters").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

    private static JsonElement Parameter(JsonElement entry, string name) => entry.GetProperty("parameters").GetProperty(name);

    private static string? TextParameter(JsonElement entry, string name) => Parameter(entry, name).GetString();

    private static string[] TextList(JsonElement entry, string name) =>
        [.. Parameter(entry, name).EnumerateArray().Select(item => item.GetString()!)];

    private static void AssertRole(JsonElement role, Guid id, string name, string? systemKind)
    {
        Assert.Equal(id, role.GetProperty("id").GetGuid());
        Assert.Equal(name, role.GetProperty("name").GetString());
        if (systemKind is null)
        {
            Assert.Equal(JsonValueKind.Null, role.GetProperty("systemKind").ValueKind);
        }
        else
        {
            Assert.Equal(systemKind, role.GetProperty("systemKind").GetString());
        }
    }

    // API-04

    // AC-367
    [Fact]
    public async Task RenameOrganization_ByOwner_WritesOneOrganizationRenamedEntry()
    {
        Scene scene = await SeedSceneAsync();

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id)
            .PatchAsJsonAsync(OrganizationPath, new { name = "Tên mới" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "OrganizationRenamed", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "nameBefore", "nameAfter");
        Assert.Equal(OrganizationName, TextParameter(entry, "nameBefore"));
        Assert.Equal("Tên mới", TextParameter(entry, "nameAfter"));
    }

    // API-05, API-26

    // AC-367, AC-368: the log is unreadable while the organization is deactivated (F4), so a system administrator
    // reactivates it before the viewer reads both entries.
    [Fact]
    public async Task DeactivateOrganization_ByOwner_WritesOneOrganizationDeactivatedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Account administrator = await SeedSystemAdministratorAsync();

        HttpResponseMessage deactivated = await ClientFor(scene.Owner, scene.Organization.Id)
            .PostAsJsonAsync($"{OrganizationPath}/deactivate", new { reason = "Tạm ngừng" });
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ReactivateAsync(ClientFor(administrator, null), scene.Organization.Id)).StatusCode);

        JsonElement[] entries = await ReadEntriesAsync(scene);

        Assert.Equal(2, entries.Length);
        AssertEntry(entries[0], "OrganizationReactivated", "SystemAdministrator", null);
        AssertEntry(entries[1], "OrganizationDeactivated", "Account", scene.Owner.Name);
        AssertParameterKeys(entries[1]);
    }

    // AC-367, AC-368
    [Fact]
    public async Task ReactivateOrganization_BySystemAdministrator_WritesOneOrganizationReactivatedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Account administrator = await SeedSystemAdministratorAsync();
        await DeactivateInDatabaseAsync(scene);

        HttpResponseMessage response = await ReactivateAsync(ClientFor(administrator, null), scene.Organization.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "OrganizationReactivated", "SystemAdministrator", null);
        AssertParameterKeys(entry);
    }

    // API-25

    // AC-368: the administrator is not a member of A and is working in C; the entry is A's, not C's.
    [Fact]
    public async Task DeactivateAsSystemAdministrator_NotMemberWorkingElsewhere_WritesEntriesInDeactivatedOrganizationOnly()
    {
        Scene scene = await SeedSceneAsync();
        Scene other = await SeedSceneAsync("Tổ chức C");
        Account administrator = await SeedSystemAdministratorAsync(other.Organization.Id);

        HttpResponseMessage deactivated = await ClientFor(administrator, other.Organization.Id).PostAsJsonAsync(
            $"/api/system/organizations/{scene.Organization.Id}/deactivate", new { reason = "Hết hợp đồng" });
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ReactivateAsync(ClientFor(administrator, other.Organization.Id), scene.Organization.Id)).StatusCode);

        JsonElement[] entries = await ReadEntriesAsync(scene);
        Assert.Equal(2, entries.Length);
        AssertEntry(entries[0], "OrganizationReactivated", "SystemAdministrator", null);
        AssertEntry(entries[1], "OrganizationDeactivated", "SystemAdministrator", null);
        Assert.Empty(await ReadEntriesAsync(other));
    }

    // AC-375: a member who is also a system administrator acts as an ordinary, named member.
    [Fact]
    public async Task DeactivateThenReactivate_SystemAdministratorWhoIsMember_BothEntriesAreAccountWithName()
    {
        Scene scene = await SeedSceneAsync();
        Account administrator = await SeedSystemAdministratorAsync(scene.Organization.Id);

        HttpResponseMessage deactivated = await ClientFor(administrator, null).PostAsJsonAsync(
            $"/api/system/organizations/{scene.Organization.Id}/deactivate", new { reason = "Tạm ngừng" });
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ReactivateAsync(ClientFor(administrator, null), scene.Organization.Id)).StatusCode);

        JsonElement[] entries = await ReadEntriesAsync(scene);
        Assert.Equal(2, entries.Length);
        AssertEntry(entries[0], "OrganizationReactivated", "Account", administrator.Name);
        AssertEntry(entries[1], "OrganizationDeactivated", "Account", administrator.Name);
    }

    // API-07

    // AC-367
    [Fact]
    public async Task ChangeMemberRoles_ByOwner_WritesOneMemberRolesChangedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Account target = await SeedMemberAsync(scene, SystemRoleKind.Member, "Thành viên");

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id).PutAsJsonAsync(
            $"{OrganizationPath}/members/{target.Id}/roles", new { roleIds = new[] { scene.Roles[SystemRoleKind.Admin].Id } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberRolesChanged", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "accountName", "email", "rolesBefore", "rolesAfter");
        Assert.Equal(target.Name, TextParameter(entry, "accountName"));
        Assert.Equal(target.Email, TextParameter(entry, "email"));
        AssertRole(
            Assert.Single(Parameter(entry, "rolesBefore").EnumerateArray()),
            scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
        AssertRole(
            Assert.Single(Parameter(entry, "rolesAfter").EnumerateArray()),
            scene.Roles[SystemRoleKind.Admin].Id, "Admin", "Admin");
    }

    // API-08

    // AC-367
    [Fact]
    public async Task RemoveMember_ByOwner_WritesOneMemberRemovedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Account target = await SeedMemberAsync(scene, SystemRoleKind.Member, "Thành viên");

        HttpResponseMessage response =
            await ClientFor(scene.Owner, scene.Organization.Id).DeleteAsync($"{OrganizationPath}/members/{target.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberRemoved", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "accountName", "email", "roles");
        Assert.Equal(target.Name, TextParameter(entry, "accountName"));
        Assert.Equal(target.Email, TextParameter(entry, "email"));
        AssertRole(
            Assert.Single(Parameter(entry, "roles").EnumerateArray()),
            scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
    }

    // API-09

    // AC-279
    [Fact]
    public async Task LeaveOrganization_ByCoOwner_WritesOneMemberLeftEntryNamingTheLeaver()
    {
        Scene scene = await SeedSceneAsync();
        Account leaver = await SeedMemberAsync(scene, SystemRoleKind.Owner, "Chủ sở hữu thứ hai");

        HttpResponseMessage response =
            await ClientFor(leaver, scene.Organization.Id).PostAsync($"{OrganizationPath}/leave", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberLeft", "Account", leaver.Name);
        AssertParameterKeys(entry, "accountName", "email");
        Assert.Equal(leaver.Name, TextParameter(entry, "accountName"));
        Assert.Equal(leaver.Email, TextParameter(entry, "email"));
    }

    // Entries outlive the actor's membership: the kind and name stay on the entry written before they left.
    [Fact]
    public async Task EntryKeepsActorKindAfterActorLeavesOrganization()
    {
        Scene scene = await SeedSceneAsync();
        Account coOwner = await SeedMemberAsync(scene, SystemRoleKind.Owner, "Chủ sở hữu thứ hai");
        HttpClient coOwnerClient = ClientFor(coOwner, scene.Organization.Id);
        Assert.Equal(
            HttpStatusCode.OK,
            (await coOwnerClient.PatchAsJsonAsync(OrganizationPath, new { name = "Tên mới" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent, (await coOwnerClient.PostAsync($"{OrganizationPath}/leave", null)).StatusCode);

        JsonElement[] entries = await ReadEntriesAsync(scene);

        Assert.Equal(2, entries.Length);
        AssertEntry(entries[0], "MemberLeft", "Account", coOwner.Name);
        AssertEntry(entries[1], "OrganizationRenamed", "Account", coOwner.Name);
    }

    // API-11, API-12

    // AC-369, AC-372
    [Fact]
    public async Task SendInvitation_ByOwner_WritesOneInvitationSentEntry()
    {
        Scene scene = await SeedSceneAsync();

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id).PostAsJsonAsync(
            $"{OrganizationPath}/invitations",
            new { email = "khach@example.com", roleId = scene.Roles[SystemRoleKind.Member].Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "InvitationSent", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "email", "role");
        Assert.Equal("khach@example.com", TextParameter(entry, "email"));
        AssertRole(Parameter(entry, "role"), scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
    }

    // AC-373: resending replaces the pending invitation; only the new one is recorded.
    [Fact]
    public async Task ResendInvitation_Pending_WritesOneInvitationSentEntry()
    {
        Scene scene = await SeedSceneAsync();
        OrganizationInvitation pending = await SeedInvitationAsync(scene, "khach@example.com", "pending-resend");

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id)
            .PostAsync($"{OrganizationPath}/invitations/{pending.Id}/resend", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "InvitationSent", "Account", scene.Owner.Name);
        Assert.Equal("khach@example.com", TextParameter(entry, "email"));
    }

    // AC-325, AC-373: the expired invitation it replaces is not mentioned.
    [Fact]
    public async Task ResendInvitation_Expired_WritesSingleInvitationSentEntry()
    {
        Scene scene = await SeedSceneAsync();
        OrganizationInvitation expired = await SeedInvitationAsync(
            scene, "khach@example.com", "expired-resend", DateTime.UtcNow.AddHours(-200));

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id)
            .PostAsync($"{OrganizationPath}/invitations/{expired.Id}/resend", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "InvitationSent", "Account", scene.Owner.Name);
        AssertRole(Parameter(entry, "role"), scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
    }

    // API-13

    // AC-272
    [Fact]
    public async Task RevokeInvitation_Pending_WritesOneInvitationRevokedEntry()
    {
        Scene scene = await SeedSceneAsync();
        OrganizationInvitation pending = await SeedInvitationAsync(scene, "khach@example.com", "pending-revoke");

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id)
            .DeleteAsync($"{OrganizationPath}/invitations/{pending.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "InvitationRevoked", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "email", "role");
        Assert.Equal("khach@example.com", TextParameter(entry, "email"));
        AssertRole(Parameter(entry, "role"), scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
    }

    // AC-268
    [Fact]
    public async Task DeleteInvitation_Expired_WritesOneInvitationDeletedEntry()
    {
        Scene scene = await SeedSceneAsync();
        OrganizationInvitation expired = await SeedInvitationAsync(
            scene, "khach@example.com", "expired-delete", DateTime.UtcNow.AddHours(-200));

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id)
            .DeleteAsync($"{OrganizationPath}/invitations/{expired.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "InvitationDeleted", "Account", scene.Owner.Name);
        Assert.Equal("khach@example.com", TextParameter(entry, "email"));
    }

    // API-16, API-17, API-18

    // AC-367
    [Fact]
    public async Task CreateRole_ByOwner_WritesOneRoleCreatedEntry()
    {
        Scene scene = await SeedSceneAsync();

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id).PostAsJsonAsync(
            $"{OrganizationPath}/roles", new { name = "Biên tập", permissionCodes = new[] { Permissions.FilesManage } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid roleId = (await response.Content.ReadJsonElementAsync()).GetProperty("id").GetGuid();
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "RoleCreated", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "role", "permissionCodes");
        AssertRole(Parameter(entry, "role"), roleId, "Biên tập", null);
        Assert.Equal([Permissions.FilesManage], TextList(entry, "permissionCodes"));
    }

    // AC-367
    [Fact]
    public async Task UpdateRole_ByOwner_WritesOneRoleUpdatedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Role role;
        await using (AppDbContext context = CreateDbContext())
        {
            role = await AuthTestHelper.SeedCustomRoleAsync(
                context, scene.Organization.Id, "Biên tập", [Permissions.FilesManage]);
        }

        HttpResponseMessage response = await ClientFor(scene.Owner, scene.Organization.Id).PutAsJsonAsync(
            $"{OrganizationPath}/roles/{role.Id}",
            new { name = "Biên tập viên", permissionCodes = new[] { Permissions.ApiKeysManage } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "RoleUpdated", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "nameBefore", "nameAfter", "permissionsAdded", "permissionsRemoved");
        Assert.Equal("Biên tập", TextParameter(entry, "nameBefore"));
        Assert.Equal("Biên tập viên", TextParameter(entry, "nameAfter"));
        Assert.Equal([Permissions.ApiKeysManage], TextList(entry, "permissionsAdded"));
        Assert.Equal([Permissions.FilesManage], TextList(entry, "permissionsRemoved"));
    }

    // AC-367
    [Fact]
    public async Task DeleteRole_ByOwner_WritesOneRoleDeletedEntry()
    {
        Scene scene = await SeedSceneAsync();
        Role role;
        await using (AppDbContext context = CreateDbContext())
        {
            role = await AuthTestHelper.SeedCustomRoleAsync(
                context, scene.Organization.Id, "Biên tập", [Permissions.FilesManage]);
        }

        HttpResponseMessage response =
            await ClientFor(scene.Owner, scene.Organization.Id).DeleteAsync($"{OrganizationPath}/roles/{role.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "RoleDeleted", "Account", scene.Owner.Name);
        AssertParameterKeys(entry, "role");
        AssertRole(Parameter(entry, "role"), role.Id, "Biên tập", null);
    }

    // API-21, API-22

    // AC-374, A1: the invitee works in B; the entry belongs to A, the organization that invited them.
    [Fact]
    public async Task AcceptInvitation_FromOtherOrganization_WritesMemberJoinedInInvitedOrganizationOnly()
    {
        Scene scene = await SeedSceneAsync();
        Scene other = await SeedSceneAsync("Tổ chức B");
        Account invitee = await SeedMemberAsync(other, SystemRoleKind.Member, "Khách mời");
        await SeedInvitationAsync(scene, invitee.Email, "accept-from-b");

        HttpResponseMessage response = await ClientFor(invitee, other.Organization.Id)
            .PostAsJsonAsync("/api/invitations/accept", new { token = "accept-from-b" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberJoined", "Account", invitee.Name);
        AssertParameterKeys(entry, "accountName", "email", "role");
        Assert.Equal(invitee.Name, TextParameter(entry, "accountName"));
        Assert.Equal(invitee.Email, TextParameter(entry, "email"));
        AssertRole(Parameter(entry, "role"), scene.Roles[SystemRoleKind.Member].Id, "Member", "Member");
        Assert.Empty(await ReadEntriesAsync(other));
    }

    // AC-374, A1
    [Fact]
    public async Task AcceptInvitation_FromPersonalContext_WritesMemberJoinedInInvitedOrganization()
    {
        Scene scene = await SeedSceneAsync();
        Account invitee = await SeedAccountAsync("Khách mời");
        await SeedInvitationAsync(scene, invitee.Email, "accept-personal");

        HttpResponseMessage response = await ClientFor(invitee, null)
            .PostAsJsonAsync("/api/invitations/accept", new { token = "accept-personal" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberJoined", "Account", invitee.Name);
        Assert.Equal(invitee.Email, TextParameter(entry, "email"));
    }

    // AC-374: the actor is the account the request has just created.
    [Fact]
    public async Task RegisterWithInvitation_NewAccount_WritesMemberJoinedWithNewAccountAsActor()
    {
        Scene scene = await SeedSceneAsync();
        await SeedInvitationAsync(scene, "moi@example.com", "register-token");

        HttpResponseMessage response = await fixture.CreateTestClient().PostAsJsonAsync(
            "/api/auth/register-with-invitation",
            new { token = "register-token", name = "Khách mới", username = $"e2e-{Guid.NewGuid():N}", password = "password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement entry = await ReadOnlyEntryAsync(scene);
        AssertEntry(entry, "MemberJoined", "Account", "Khách mới");
        Assert.Equal("Khách mới", TextParameter(entry, "accountName"));
        Assert.Equal("moi@example.com", TextParameter(entry, "email"));
    }

    // Operations that write nothing

    // AC-370
    [Fact]
    public async Task RejectedOperations_SelfRemovalAndMissingPermission_LeaveTheNewestEntryUnchanged()
    {
        Scene scene = await SeedSceneAsync();
        Account plainMember = await SeedMemberAsync(scene, SystemRoleKind.Member, "Thành viên");
        HttpClient owner = ClientFor(scene.Owner, scene.Organization.Id);
        Assert.Equal(
            HttpStatusCode.OK, (await owner.PatchAsJsonAsync(OrganizationPath, new { name = "Tên mới" })).StatusCode);
        long newestBefore = (await ReadNewestAsync(scene)).GetProperty("id").GetInt64();

        HttpResponseMessage selfRemoval = await owner.DeleteAsync($"{OrganizationPath}/members/{scene.Owner.Id}");
        HttpResponseMessage withoutPermission = await ClientFor(plainMember, scene.Organization.Id).PostAsJsonAsync(
            $"{OrganizationPath}/invitations",
            new { email = "khach@example.com", roleId = scene.Roles[SystemRoleKind.Member].Id });

        Assert.Equal(HttpStatusCode.Conflict, selfRemoval.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutPermission.StatusCode);
        JsonElement[] entries = await ReadEntriesAsync(scene);
        JsonElement newest = Assert.Single(entries);
        Assert.Equal(newestBefore, newest.GetProperty("id").GetInt64());
        AssertEntry(newest, "OrganizationRenamed", "Account", scene.Owner.Name);
    }

    // AC-364
    [Fact]
    public async Task CreateOrganization_ByAccount_WritesNoEntryInTheNewLog()
    {
        Account creator = await SeedAccountAsync("Người tạo");

        HttpResponseMessage response = await ClientFor(creator, null).PostAsJsonAsync(
            "/api/organizations", new CreateOrganizationRequest("Acme Inc", $"acme-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid organizationId = (await response.Content.ReadJsonElementAsync()).GetProperty("id").GetGuid();
        HttpResponseMessage log = await ClientFor(creator, organizationId).GetAsync(LogPath);
        Assert.Equal(HttpStatusCode.OK, log.StatusCode);
        JsonElement page = await log.Content.ReadJsonElementAsync();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    // The log is unreadable while the organization is deactivated, which is why the tests above reactivate first.
    [Fact]
    public async Task GetAuditLog_DeactivatedOrganization_Returns403OrganizationSuspended()
    {
        Scene scene = await SeedSceneAsync();
        await DeactivateInDatabaseAsync(scene);

        HttpResponseMessage response = await ClientFor(scene.Viewer, scene.Organization.Id).GetAsync(LogPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            ApplicationMessages.OrganizationSuspended,
            (await response.Content.ReadJsonElementAsync()).GetProperty("code").GetString());
    }
}
