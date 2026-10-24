using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CurrentOrganizationMembersTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/organization/members";

    private static readonly string EntityNotFoundCode = new NotFoundException(nameof(Role), Guid.Empty).Message;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<Account> SeedAccountAsync(string name)
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"member-{Guid.NewGuid():N}";
        return await AuthTestHelper.SeedConfirmedAccountAsync(context, username: handle, email: $"{handle}@example.com", name: name);
    }

    private async Task<Organization> SeedOrganizationAsync()
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedOrganizationAsync(context, name: "Bảo Minh");
    }

    private async Task<OrganizationMember> SeedMemberAsync(Guid organizationId, Guid accountId, SystemRoleKind role)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedOrganizationMemberAsync(context, organizationId, accountId, role);
    }

    private async Task<IReadOnlyDictionary<SystemRoleKind, Role>> SystemRolesAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedSystemRolesAsync(context, organizationId);
    }

    private async Task<int> CountEventsAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs.CountAsync(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event);
    }

    private async Task<List<Guid>> RoleIdsOfAsync(Guid organizationMemberId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.OrganizationMemberRoles
            .Where(link => link.OrganizationMemberId == organizationMemberId)
            .Select(link => link.RoleId)
            .ToListAsync();
    }

    private async Task<bool> IsActiveAsync(Guid organizationMemberId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.OrganizationMembers
            .Where(member => member.Id == organizationMemberId)
            .Select(member => member.IsActive)
            .SingleAsync();
    }

    /// <summary>An organization with an Owner caller and one plain member, both with clients.</summary>
    private async Task<(Organization Organization, Account Owner, OrganizationMember OwnerMember, Account Plain,
        OrganizationMember PlainMember)> SeedOwnerAndPlainAsync()
    {
        Organization organization = await SeedOrganizationAsync();
        Account owner = await SeedAccountAsync("An Chủ");
        Account plain = await SeedAccountAsync("Bình Thường");
        OrganizationMember ownerMember = await SeedMemberAsync(organization.Id, owner.Id, SystemRoleKind.Owner);
        OrganizationMember plainMember = await SeedMemberAsync(organization.Id, plain.Id, SystemRoleKind.Member);
        return (organization, owner, ownerMember, plain, plainMember);
    }

    private static Task<HttpResponseMessage> ChangeRolesAsync(HttpClient client, Guid accountId, object body) =>
        client.PutAsJsonAsync($"{Path}/{accountId}/roles", body);

    private static Task<HttpResponseMessage> SendWriteAsync(HttpClient client, string write, Guid accountId) =>
        write switch
        {
            "change-roles" => ChangeRolesAsync(client, accountId, new { roleIds = new[] { Guid.NewGuid() } }),
            "remove" => client.DeleteAsync($"{Path}/{accountId}"),
            _ => client.PostAsync("/api/organization/leave", null)
        };

    private HttpClient ClientFor(Account account, Guid? organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    // Authorization

    [Fact]
    public async Task Members_Anonymous_Returns401()
    {
        HttpResponseMessage response = await fixture.CreateTestClient().GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // An API key never stands in an organization, and this route hides existence behind 404.
    [Fact]
    public async Task Members_ApiKey_Returns404ResourceNotFound()
    {
        Organization organization = await SeedOrganizationAsync();
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "members-api-key");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.NotFound, ApplicationMessages.ResourceNotFound);
    }

    // AC-122, BR-45
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Members_PersonalContextOrNonMemberSystemAdministrator_Returns404ResourceNotFound(
        bool nonMemberSystemAdministrator)
    {
        Account caller = await SeedAccountAsync("Người ngoài");
        Organization organization = await SeedOrganizationAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            if (nonMemberSystemAdministrator)
            {
                context.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(caller.Id)));
                await context.SaveChangesAsync();
            }
        }

        HttpClient client = ClientFor(caller, nonMemberSystemAdministrator ? organization.Id : null);

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.NotFound, ApplicationMessages.ResourceNotFound);
    }

    // AC-301
    [Fact]
    public async Task Members_SuspendedOrganizationMember_Returns403Suspended()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("pageNumber=0")]
    public async Task Members_PageOutOfRange_Returns400(string query)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Owner);

        HttpResponseMessage response = await client.GetAsync($"{Path}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // List

    // AC-261, AC-262
    [Fact]
    public async Task Members_PlainMemberResponse_HasNoOtherEmails()
    {
        Organization organization = await SeedOrganizationAsync();
        Account owner = await SeedAccountAsync("An Chủ");
        Account plain = await SeedAccountAsync("Bình Thường");
        await SeedMemberAsync(organization.Id, owner.Id, SystemRoleKind.Owner);
        await SeedMemberAsync(organization.Id, plain.Id, SystemRoleKind.Member);

        HttpResponseMessage response = await ClientFor(plain, organization.Id).GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.Email, raw);
        Assert.Contains(plain.Email, raw);
        JsonElement items = JsonDocument.Parse(raw).RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        foreach (JsonElement item in items.EnumerateArray())
        {
            Assert.False(item.GetProperty("canChangeRoles").GetBoolean());
            Assert.False(item.GetProperty("canRemove").GetBoolean());
        }
    }

    // AC-263, AC-338, AC-339
    [Fact]
    public async Task Members_Admin_SeesEmailsOrderedByNameAndOwnerRowLocked()
    {
        Organization organization = await SeedOrganizationAsync();
        Account owner = await SeedAccountAsync("Âu Cơ");
        Account admin = await SeedAccountAsync("an Bình");
        Account member = await SeedAccountAsync("Bảo Minh");
        await SeedMemberAsync(organization.Id, owner.Id, SystemRoleKind.Owner);
        await SeedMemberAsync(organization.Id, admin.Id, SystemRoleKind.Admin);
        await SeedMemberAsync(organization.Id, member.Id, SystemRoleKind.Member);

        HttpResponseMessage response = await ClientFor(admin, organization.Id).GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(
            ["an Bình", "Âu Cơ", "Bảo Minh"], items.Select(item => item.GetProperty("accountName").GetString()));
        Assert.Equal(
            [admin.Email, owner.Email, member.Email], items.Select(item => item.GetProperty("email").GetString()));
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());

        Assert.True(items[0].GetProperty("isSelf").GetBoolean());
        Assert.True(items[0].GetProperty("canChangeRoles").GetBoolean());
        Assert.False(items[0].GetProperty("canRemove").GetBoolean());

        Assert.False(items[1].GetProperty("canChangeRoles").GetBoolean());
        Assert.False(items[1].GetProperty("canRemove").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("lockRole").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, items[1].GetProperty("lockReason").ValueKind);
        Assert.Equal("Owner", items[1].GetProperty("roleNames")[0].GetString());

        Assert.True(items[2].GetProperty("canChangeRoles").GetBoolean());
        Assert.True(items[2].GetProperty("canRemove").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("lockReason").ValueKind);
    }

    // Task 6 review carry: emails follow the database, not a token or permission cache that still says "manage".
    [Fact]
    public async Task Members_AdminWhoseManageWasRemovedInDatabase_SeesNoOtherEmails()
    {
        Organization organization = await SeedOrganizationAsync();
        Account owner = await SeedAccountAsync("Âu Cơ");
        Account admin = await SeedAccountAsync("an Bình");
        await SeedMemberAsync(organization.Id, owner.Id, SystemRoleKind.Owner);
        await SeedMemberAsync(organization.Id, admin.Id, SystemRoleKind.Admin);
        HttpClient client = ClientFor(admin, organization.Id);
        Assert.Contains(owner.Email, await (await client.GetAsync(Path)).Content.ReadAsStringAsync());
        Role adminRole = (await SystemRolesAsync(organization.Id))[SystemRoleKind.Admin];
        await using (AppDbContext context = CreateDbContext())
        {
            await context.RolePermissions
                .Where(permission => permission.RoleId == adminRole.Id
                    && permission.PermissionCode == Permissions.OrganizationMembersManage)
                .ExecuteDeleteAsync();
        }

        HttpResponseMessage response = await client.GetAsync(Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.Email, raw);
        Assert.Contains(admin.Email, raw);
        foreach (JsonElement item in JsonDocument.Parse(raw).RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.False(item.GetProperty("canChangeRoles").GetBoolean());
            Assert.False(item.GetProperty("canRemove").GetBoolean());
        }
    }

    // Writes: authorization

    [Theory]
    [InlineData("change-roles")]
    [InlineData("remove")]
    [InlineData("leave")]
    public async Task MemberWrites_Anonymous_Return401(string write)
    {
        HttpResponseMessage response = await SendWriteAsync(fixture.CreateTestClient(), write, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // An API key never stands in an organization (TECH00 §5.2).
    [Theory]
    [InlineData("change-roles")]
    [InlineData("remove")]
    [InlineData("leave")]
    public async Task MemberWrites_ApiKey_Return403AccessDenied(string write)
    {
        (Organization organization, _, _, Account plain, _) = await SeedOwnerAndPlainAsync();
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, $"members-{write}");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await SendWriteAsync(client, write, plain.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // AC-264
    [Theory]
    [InlineData("change-roles")]
    [InlineData("remove")]
    public async Task ChangeRolesOrRemove_PlainMember_Returns403(string write)
    {
        (Organization organization, Account owner, OrganizationMember ownerMember, Account plain, _) =
            await SeedOwnerAndPlainAsync();

        HttpResponseMessage response = await SendWriteAsync(ClientFor(plain, organization.Id), write, owner.Id);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.True(await IsActiveAsync(ownerMember.Id));
    }

    // ChangeMemberRoles

    [Fact]
    public async Task ChangeRoles_Owner_Returns204AndRecordsEvent()
    {
        (Organization organization, Account owner, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();
        Role admin = (await SystemRolesAsync(organization.Id))[SystemRoleKind.Admin];

        HttpResponseMessage response = await ChangeRolesAsync(
            ClientFor(owner, organization.Id), plain.Id, new { roleIds = new[] { admin.Id } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([admin.Id], await RoleIdsOfAsync(plainMember.Id));
        await using AppDbContext context = CreateDbContext();
        AuditLog entry = await context.AuditLogs.SingleAsync(
            log => log.OrganizationId == organization.Id && log.Kind == AuditLogKind.Event);
        Assert.Equal(nameof(AuditEventType.MemberRolesChanged), entry.Action);
        Assert.Equal(owner.Id.ToString(), entry.UserId);
    }

    [Fact]
    public async Task ChangeRoles_RoleIdOfOtherOrganization_Returns404AndNothingChanges()
    {
        (Organization organization, Account owner, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();
        Organization other = await SeedOrganizationAsync();
        Role foreignAdmin = (await SystemRolesAsync(other.Id))[SystemRoleKind.Admin];
        List<Guid> before = await RoleIdsOfAsync(plainMember.Id);

        HttpResponseMessage response = await ChangeRolesAsync(
            ClientFor(owner, organization.Id), plain.Id, new { roleIds = new[] { foreignAdmin.Id } });

        await AssertCodeAsync(response, HttpStatusCode.NotFound, EntityNotFoundCode);
        Assert.Equal(before, await RoleIdsOfAsync(plainMember.Id));
        Assert.Equal(0, await CountEventsAsync(organization.Id));
    }

    [Fact]
    public async Task ChangeRoles_SameRoleSet_Returns204AndAuditLogHasNoNewRow()
    {
        (Organization organization, Account owner, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();
        List<Guid> before = await RoleIdsOfAsync(plainMember.Id);

        HttpResponseMessage response = await ChangeRolesAsync(
            ClientFor(owner, organization.Id), plain.Id, new { roleIds = before.Concat(before).ToArray() });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await RoleIdsOfAsync(plainMember.Id));
        Assert.Equal(0, await CountEventsAsync(organization.Id));
    }

    [Fact]
    public async Task ChangeRoles_EmptyRoleIds_Returns400RequiresAtLeastOneRole()
    {
        (Organization organization, Account owner, _, Account plain, _) = await SeedOwnerAndPlainAsync();

        HttpResponseMessage response = await ChangeRolesAsync(
            ClientFor(owner, organization.Id), plain.Id, new { roleIds = Array.Empty<Guid>() });

        await AssertCodeAsync(
            response, HttpStatusCode.BadRequest, ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole);
    }

    // F13: a missing list never reaches the service; model validation answers it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangeRoles_NullOrMissingRoleIds_Returns400ValidationErrorOnRoleIds(bool missing)
    {
        (Organization organization, Account owner, _, Account plain, _) = await SeedOwnerAndPlainAsync();
        object body = missing ? new { } : new { roleIds = (Guid[]?)null };

        HttpResponseMessage response = await ChangeRolesAsync(ClientFor(owner, organization.Id), plain.Id, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement problem = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "RoleIds", StringComparison.OrdinalIgnoreCase));
    }

    // RemoveMember

    // AC-337, AC-370
    [Fact]
    public async Task Remove_Self_Returns409WithMessageAndNoNewAuditRow()
    {
        (Organization organization, Account owner, OrganizationMember ownerMember, _, _) =
            await SeedOwnerAndPlainAsync();

        HttpResponseMessage response = await ClientFor(owner, organization.Id).DeleteAsync($"{Path}/{owner.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(ApplicationMessages.CannotRemoveSelf, body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
        Assert.True(await IsActiveAsync(ownerMember.Id));
        Assert.Equal(0, await CountEventsAsync(organization.Id));
    }

    // D2: without the permission the gate answers 403 before the self check could answer 409.
    [Fact]
    public async Task Remove_SelfWithoutMembersManage_Returns403()
    {
        (Organization organization, _, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();

        HttpResponseMessage response = await ClientFor(plain, organization.Id).DeleteAsync($"{Path}/{plain.Id}");

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.True(await IsActiveAsync(plainMember.Id));
    }

    // T6 session: a removed member's refresh lands in the personal context, not back in the organization.
    [Fact]
    public async Task Remove_ThenRemovedMemberRefreshes_ReturnsNullOrganization()
    {
        (Organization organization, Account owner, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();
        const string RawToken = "removed-member-refresh-token";
        await using (AppDbContext context = CreateDbContext())
        {
            context.RefreshTokens.Add(RefreshToken.Create(new RefreshTokenParams(
                plain.Id, Sha256(RawToken), DateTime.UtcNow.AddDays(1), null, null, true,
                DateTime.UtcNow.AddHours(-1), organization.Id, Guid.NewGuid())));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage removed = await ClientFor(owner, organization.Id).DeleteAsync($"{Path}/{plain.Id}");
        HttpResponseMessage refreshed = await fixture.CreateTestClient()
            .PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(RawToken));

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False(await IsActiveAsync(plainMember.Id));
        Assert.Empty(await RoleIdsOfAsync(plainMember.Id));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        LoginResponse? body = await refreshed.Content.ReadJsonAsync<LoginResponse>();
        Assert.Null(body!.OrganizationId);
    }

    // Leave

    // The first request caches the caller's access; leaving must drop it, or the old token would still get in.
    [Fact]
    public async Task Leave_ThenNextRequestWithOldToken_Returns403AccessDenied()
    {
        (Organization organization, _, _, Account plain, OrganizationMember plainMember) =
            await SeedOwnerAndPlainAsync();
        HttpClient client = ClientFor(plain, organization.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/organization")).StatusCode);

        HttpResponseMessage left = await client.PostAsync("/api/organization/leave", null);
        HttpResponseMessage after = await client.GetAsync("/api/organization");

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.False(await IsActiveAsync(plainMember.Id));
        await AssertCodeAsync(after, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Equal(1, await CountEventsAsync(organization.Id));
    }

    // AC-277
    [Fact]
    public async Task Leave_LastOwner_Returns409()
    {
        (Organization organization, Account owner, OrganizationMember ownerMember, _, _) =
            await SeedOwnerAndPlainAsync();

        HttpResponseMessage response = await ClientFor(owner, organization.Id).PostAsync("/api/organization/leave", null);

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.LastOwnerCannotLeave);
        Assert.True(await IsActiveAsync(ownerMember.Id));
        Assert.Equal(0, await CountEventsAsync(organization.Id));
    }

    private static string Sha256(string input) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
