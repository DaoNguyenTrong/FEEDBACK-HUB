using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Controllers;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class OrganizationAuditLogTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/organization/audit-log";

    private static readonly DateTime Newest = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private HttpClient ClientFor(Account account, Guid? organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private async Task<Account> SeedAccountAsync(string name = "Người xem")
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"audit-{Guid.NewGuid():N}";
        return await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: handle, email: $"{handle}@example.com", name: name);
    }

    // A member whose only permissions come from a custom role holding exactly the given codes.
    private async Task<(Organization Organization, Account Viewer)> SeedViewerAsync(params string[] permissionCodes)
    {
        Account viewer = await SeedAccountAsync();
        await using AppDbContext context = CreateDbContext();
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: "Bảo Minh");
        OrganizationMember member = await AuthTestHelper.SeedOrganizationMemberAsync(
            context, organization.Id, viewer.Id, SystemRoleKind.Member);
        Role role = await AuthTestHelper.SeedCustomRoleAsync(context, organization.Id, "Người xem nhật ký", permissionCodes);
        await AuthTestHelper.AssignRoleAsync(context, member.Id, role.Id);
        return (organization, viewer);
    }

    private async Task<(Organization Organization, Account Viewer)> SeedViewerAsync() =>
        await SeedViewerAsync(Permissions.AuditLogsView);

    private async Task<AuditLog> SeedRowAsync(
        Guid? organizationId,
        DateTime timestamp,
        AuditLogKind kind = AuditLogKind.Event,
        string entityName = "OrganizationMember",
        string action = "MemberLeft",
        string? actorKind = "Account",
        string? userId = null,
        string? newValues = """{"accountName":"Lan","email":"lan@example.com"}""")
    {
        AuditLog row = new()
        {
            EntityName = entityName,
            EntityId = Guid.NewGuid().ToString(),
            Action = action,
            OldValues = "{\"secret\":\"old\"}",
            NewValues = newValues,
            UserId = userId,
            OrganizationId = organizationId,
            IpAddress = "203.0.113.9",
            UserAgent = "audit-test-agent",
            Timestamp = timestamp,
            Kind = kind,
            ActorKind = actorKind
        };
        await using AppDbContext context = CreateDbContext();
        context.AuditLogs.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    private static async Task<JsonElement> AssertCodeAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }

    private static async Task<JsonElement> ReadPageAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadJsonElementAsync();
    }

    private static string Base64UrlEncode(string payload) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static long[] IdsOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];

    // Authorization

    [Fact]
    public async Task AuditLog_Anonymous_Returns401()
    {
        HttpResponseMessage response = await fixture.CreateTestClient().GetAsync(Path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC-366
    [Fact]
    public async Task AuditLog_PersonalContext_Returns403OrganizationAccessDenied()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, personalContext: true);

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // AC-362
    [Theory]
    [InlineData(SystemRoleKind.Member)]
    [InlineData(SystemRoleKind.Admin)]
    public async Task AuditLog_MemberWithoutAuditLogsView_Returns403(SystemRoleKind role)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, role);

        HttpResponseMessage response = await client.GetAsync(Path);

        JsonElement body = await AssertCodeAsync(
            response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.False(body.TryGetProperty("organizationName", out _));
    }

    [Fact]
    public async Task AuditLog_OwnerHoldsAuditLogsView_Returns200()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Owner);

        HttpResponseMessage response = await client.GetAsync(Path);

        JsonElement page = await ReadPageAsync(response);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    // B7: an API key is a credential of the organization, never of a person who may read its log.
    [Fact]
    public async Task AuditLog_ApiKey_Returns403()
    {
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            Organization organization = await AuthTestHelper.SeedOrganizationAsync(context);
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "audit-api-key");
            await SeedRowAsync(organization.Id, Newest);
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    [Fact]
    public async Task AuditLog_SystemAdministratorNotMember_Returns403OrganizationAccessDenied()
    {
        Account administrator = await SeedAccountAsync("Quản trị hệ thống");
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            context.SystemAdministrators.Add(
                SystemAdministrator.Create(new SystemAdministratorParams(administrator.Id)));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await ClientFor(administrator, organization.Id).GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    [Fact]
    public async Task AuditLog_SuspendedOrganization_Returns403OrganizationSuspended()
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(
            fixture, SystemRoleKind.Owner, organizationSuspended: true);

        HttpResponseMessage response = await client.GetAsync(Path);

        JsonElement body = await AssertCodeAsync(
            response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationSuspended);
        Assert.Equal(AuthTestHelper.GateOrganizationName, body.GetProperty("organizationName").GetString());
    }

    // The token and the permission cache still say "view"; the service re-reads it from the database (BR-75).
    [Fact]
    public async Task AuditLog_PermissionRevokedWhileCacheStale_Returns403()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        await SeedRowAsync(organization.Id, Newest);
        HttpClient client = ClientFor(viewer, organization.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path)).StatusCode);
        await using (AppDbContext context = CreateDbContext())
        {
            await context.RolePermissions
                .Where(permission => permission.PermissionCode == Permissions.AuditLogsView
                    && context.Roles.Any(role => role.Id == permission.RoleId
                        && role.OrganizationId == organization.Id))
                .ExecuteDeleteAsync();
        }

        HttpResponseMessage response = await client.GetAsync(Path);

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
    }

    // The service re-reads the permission and answers the same 403, which would hide a weaker endpoint policy from
    // every HTTP test here; the gate's own check is pinned instead.
    [Fact]
    public void GetAuditLog_Action_RequiresAuditLogsViewPolicy()
    {
        MethodInfo method = typeof(CurrentOrganizationController).GetMethod(nameof(CurrentOrganizationController.GetAuditLog))
            ?? throw new InvalidOperationException("No audit-log action.");

        AuthorizeAttribute authorize = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>(true));
        Assert.Equal(Permissions.AuditLogsView, authorize.Policy);
    }

    // Query validation

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=-1")]
    public async Task AuditLog_LimitOutsideRange_Returns400(string query)
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        await SeedRowAsync(organization.Id, Newest);

        HttpResponseMessage response = await ClientFor(viewer, organization.Id).GetAsync($"{Path}?{query}");

        JsonElement problem = await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.ValidationFailed);
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "limit", StringComparison.OrdinalIgnoreCase));
    }

    // The payload is encoded in the test body; the first three rows are literal strings that are not a cursor.
    [Theory]
    [InlineData("@@@", false)]
    [InlineData("A=", false)]
    [InlineData("bm90LWEtY3Vyc29y", false)]
    [InlineData("1:0", true)]
    [InlineData("99999999999999999999:1", true)]
    public async Task AuditLog_MalformedCursor_Returns400InvalidCursor(string value, bool encode)
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        string cursor = encode ? Base64UrlEncode(value) : value;

        HttpResponseMessage response = await ClientFor(viewer, organization.Id)
            .GetAsync($"{Path}?cursor={Uri.EscapeDataString(cursor)}");

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.InvalidCursor);
    }

    [Fact]
    public async Task AuditLog_OverlongCursor_Returns400InvalidCursor()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();

        HttpResponseMessage response = await ClientFor(viewer, organization.Id)
            .GetAsync($"{Path}?cursor={new string('A', 100)}");

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.InvalidCursor);
    }

    // Content

    // AC-363, T19
    [Fact]
    public async Task AuditLog_MixedRows_ReturnsOnlyEventEntriesOfCurrentOrganization()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        Organization other;
        await using (AppDbContext context = CreateDbContext())
        {
            other = await AuthTestHelper.SeedOrganizationAsync(context);
        }

        AuditLog own = await SeedRowAsync(organization.Id, Newest);
        await SeedRowAsync(organization.Id, Newest.AddMinutes(1), kind: AuditLogKind.Change);
        await SeedRowAsync(null, Newest.AddMinutes(2));
        await SeedRowAsync(other.Id, Newest.AddMinutes(3));

        JsonElement page = await ReadPageAsync(await ClientFor(viewer, organization.Id).GetAsync(Path));

        Assert.Equal([own.Id], IdsOf(page));
    }

    // A cursor is only a position; the organization always comes from the session.
    [Fact]
    public async Task AuditLog_CursorFromOtherOrganization_ReturnsOnlyCurrentOrganizationRows()
    {
        (Organization organizationA, Account viewerA) = await SeedViewerAsync();
        (Organization organizationB, Account viewerB) = await SeedViewerAsync();
        AuditLog a1 = await SeedRowAsync(organizationA.Id, Newest);
        await SeedRowAsync(organizationA.Id, Newest.AddMinutes(-1));
        AuditLog b1 = await SeedRowAsync(organizationB.Id, Newest.AddMinutes(-5));
        AuditLog b2 = await SeedRowAsync(organizationB.Id, Newest.AddMinutes(-6));

        JsonElement pageA = await ReadPageAsync(await ClientFor(viewerA, organizationA.Id).GetAsync($"{Path}?limit=1"));
        string cursorFromA = pageA.GetProperty("nextCursor").GetString()!;
        Assert.Equal([a1.Id], IdsOf(pageA));

        JsonElement pageB = await ReadPageAsync(await ClientFor(viewerB, organizationB.Id)
            .GetAsync($"{Path}?cursor={Uri.EscapeDataString(cursorFromA)}"));

        Assert.Equal([b1.Id, b2.Id], IdsOf(pageB));
    }

    // AC-371
    [Fact]
    public async Task AuditLog_Response_OmitsIpUserAgentEntityIdAndRawValues()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        AuditLog row = await SeedRowAsync(organization.Id, Newest);

        HttpResponseMessage response = await ClientFor(viewer, organization.Id).GetAsync(Path);

        string json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"id\"", json);
        foreach (string forbidden in new[] { "ipAddress", "userAgent", "entityId", "oldValues", "newValues" })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(row.EntityId, json);
        Assert.DoesNotContain("203.0.113.9", json);
        Assert.DoesNotContain("audit-test-agent", json);
    }

    [Fact]
    public async Task AuditLog_EnumFields_SerializeAsNames()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        Account actor = await SeedAccountAsync("Người mời");
        await SeedRowAsync(
            organization.Id,
            Newest,
            entityName: "OrganizationInvitation",
            action: "InvitationSent",
            userId: actor.Id.ToString(),
            newValues: "{\"email\":\"moi@example.com\",\"role\":{\"id\":\"" + Guid.NewGuid()
                + "\",\"name\":\"Member\",\"systemKind\":\"Member\"}}");

        JsonElement page = await ReadPageAsync(await ClientFor(viewer, organization.Id).GetAsync(Path));

        JsonElement item = page.GetProperty("items")[0];
        Assert.Equal("Account", item.GetProperty("actorKind").GetString());
        Assert.Equal("InvitationSent", item.GetProperty("type").GetString());
        Assert.Equal("Người mời", item.GetProperty("actorName").GetString());
        Assert.Equal("Member", item.GetProperty("parameters").GetProperty("role").GetProperty("systemKind").GetString());
    }

    // AC-335, D12: the log's own permission is enough to read emails; organizations.members.manage is not needed.
    [Fact]
    public async Task AuditLog_ViewerWithoutMembersManage_SeesEmailInParameters()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        await SeedRowAsync(organization.Id, Newest);

        JsonElement page = await ReadPageAsync(await ClientFor(viewer, organization.Id).GetAsync(Path));

        JsonElement parameters = page.GetProperty("items")[0].GetProperty("parameters");
        Assert.Equal("lan@example.com", parameters.GetProperty("email").GetString());
        Assert.Equal("Lan", parameters.GetProperty("accountName").GetString());
    }

    // AC-360
    [Fact]
    public async Task AuditLog_TwoPages_ReturnsNewestFirstWithNextCursor()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        AuditLog[] rows =
        [
            await SeedRowAsync(organization.Id, Newest.AddMinutes(-4)),
            await SeedRowAsync(organization.Id, Newest.AddMinutes(-2)),
            await SeedRowAsync(organization.Id, Newest),
            await SeedRowAsync(organization.Id, Newest.AddMinutes(-1)),
            await SeedRowAsync(organization.Id, Newest.AddMinutes(-3))
        ];
        long[] newestFirst = [.. rows.OrderByDescending(row => row.Timestamp).Select(row => row.Id)];
        HttpClient client = ClientFor(viewer, organization.Id);

        JsonElement first = await ReadPageAsync(await client.GetAsync($"{Path}?limit=2"));
        string cursor = first.GetProperty("nextCursor").GetString()!;
        JsonElement second = await ReadPageAsync(await client.GetAsync($"{Path}?limit=2&cursor={Uri.EscapeDataString(cursor)}"));
        string cursor2 = second.GetProperty("nextCursor").GetString()!;
        JsonElement third = await ReadPageAsync(await client.GetAsync($"{Path}?limit=2&cursor={Uri.EscapeDataString(cursor2)}"));

        Assert.Equal(newestFirst[..2], IdsOf(first));
        Assert.Equal(newestFirst[2..4], IdsOf(second));
        Assert.Equal(newestFirst[4..], IdsOf(third));
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
    }

    // The cursor is opaque to clients, but its documented shape must keep decoding.
    [Fact]
    public async Task AuditLog_NextCursor_IsUnpaddedBase64UrlOfTicksAndId()
    {
        (Organization organization, Account viewer) = await SeedViewerAsync();
        AuditLog newest = await SeedRowAsync(organization.Id, Newest);
        await SeedRowAsync(organization.Id, Newest.AddMinutes(-1));

        JsonElement page = await ReadPageAsync(await ClientFor(viewer, organization.Id).GetAsync($"{Path}?limit=1"));

        string cursor = page.GetProperty("nextCursor").GetString()!;
        Assert.DoesNotContain('=', cursor);
        string decoded = Encoding.UTF8.GetString(System.Buffers.Text.Base64Url.DecodeFromChars(cursor));
        Assert.Equal($"{newest.Timestamp.Ticks}:{newest.Id}", decoded);
    }

    // The old route is gone for good, not moved behind another policy.
    [Theory]
    [InlineData("/api/admin/audit-logs")]
    [InlineData("/api/admin/audit-logs/1")]
    public async Task OldAdminAuditLogsRoutes_SignedIn_Return404(string path)
    {
        HttpClient client = await AuthTestHelper.CreateOrganizationCallerClientAsync(fixture, SystemRoleKind.Owner);

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
