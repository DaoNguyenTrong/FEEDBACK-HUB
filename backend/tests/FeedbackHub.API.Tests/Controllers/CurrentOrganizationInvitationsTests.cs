using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public sealed partial class CurrentOrganizationInvitationsTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/organization/invitations";

    private static readonly string EntityNotFoundCode = new NotFoundException(nameof(Role), Guid.Empty).Message;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<Account> SeedAccountAsync(string name)
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"inviter-{Guid.NewGuid():N}";
        return await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: handle, email: $"{handle}@example.com", name: name);
    }

    private async Task<(Organization Organization, Account Caller, IReadOnlyDictionary<SystemRoleKind, Role> Roles)>
        SeedCallerAsync(SystemRoleKind role)
    {
        Account caller = await SeedAccountAsync("Người mời");
        await using AppDbContext context = CreateDbContext();
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: "Bảo Minh");
        await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, role);
        IReadOnlyDictionary<SystemRoleKind, Role> roles =
            await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
        return (organization, caller, roles);
    }

    private async Task<OrganizationInvitation> SeedInvitationAsync(
        Guid organizationId, string email, Guid roleId, string rawToken)
    {
        await using AppDbContext context = CreateDbContext();
        OrganizationInvitation invitation = OrganizationInvitation.Create(new OrganizationInvitationParams(
            organizationId, email, roleId, Sha256(rawToken), DateTime.UtcNow));
        context.OrganizationInvitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation;
    }

    private async Task<List<OrganizationInvitation>> InvitationsOfAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.OrganizationInvitations
            .Where(invitation => invitation.OrganizationId == organizationId)
            .ToListAsync();
    }

    private async Task<List<AuditLog>> EventsOfAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs
            .Where(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event)
            .ToListAsync();
    }

    private HttpClient ClientFor(Account account, Guid? organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(Path, body);

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonElement body = await response.Content.ReadJsonElementAsync();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    // The stored hash, computed as TokenHash does (internal to Application).
    private static string Sha256(string input) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    [GeneratedRegex("#/invite\\?token=([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    // Send: authorization

    [Fact]
    public async Task Send_Anonymous_Returns401()
    {
        HttpResponseMessage response =
            await SendAsync(fixture.CreateTestClient(), new { email = "b@example.com", roleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC-316
    [Fact]
    public async Task Send_MemberWithoutMembersManage_Returns403AndSendsNothing()
    {
        (Organization organization, Account caller, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Member);

        HttpResponseMessage response = await SendAsync(
            ClientFor(caller, organization.Id), new { email = "b@example.com", roleId = roles[SystemRoleKind.Member].Id });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }

    // An API key never stands in an organization (TECH00 §5.2).
    [Fact]
    public async Task Send_ApiKey_Returns403AccessDenied()
    {
        (Organization organization, _, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Owner);
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "invitations-api-key");
        }

        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response =
            await SendAsync(client, new { email = "b@example.com", roleId = roles[SystemRoleKind.Member].Id });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }

    // F2: the token and the permission cache still say "manage"; the service re-reads it after the lock.
    [Fact]
    public async Task Send_ManageRemovedInDatabaseWhileCached_Returns403NoInvitationNoEmail()
    {
        (Organization organization, Account admin, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Admin);
        HttpClient client = ClientFor(admin, organization.Id);
        // role-options requires members.manage, so this caches it for the caller.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/organization/role-options")).StatusCode);
        await using (AppDbContext context = CreateDbContext())
        {
            await context.RolePermissions
                .Where(permission => permission.RoleId == roles[SystemRoleKind.Admin].Id
                    && permission.PermissionCode == Permissions.OrganizationMembersManage)
                .ExecuteDeleteAsync();
        }

        HttpResponseMessage response =
            await SendAsync(client, new { email = "b@example.com", roleId = roles[SystemRoleKind.Member].Id });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.OrganizationAccessDenied);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
        Assert.Empty(await EventsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }

    // Send: input

    [Fact]
    public async Task Send_InvalidEmail_Returns400FieldEmailAddress()
    {
        (Organization organization, Account owner, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await SendAsync(
            ClientFor(owner, organization.Id), new { email = "not-an-email", roleId = roles[SystemRoleKind.Member].Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        JsonElement problem = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(ApplicationMessages.ValidationFailed, problem.GetProperty("code").GetString());
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "Email", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await InvitationsOfAsync(organization.Id));
    }

    // F18: [Required] never fails on a Guid, so a missing roleId binds Guid.Empty and answers 404, not 500.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Send_MissingOrEmptyRoleId_Returns404(bool missing)
    {
        (Organization organization, Account owner, _) = await SeedCallerAsync(SystemRoleKind.Owner);
        object body = missing ? new { email = "b@example.com" } : new { email = "b@example.com", roleId = Guid.Empty };

        HttpResponseMessage response = await SendAsync(ClientFor(owner, organization.Id), body);

        await AssertCodeAsync(response, HttpStatusCode.NotFound, EntityNotFoundCode);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }

    [Fact]
    public async Task Send_RoleOfOtherOrganization_Returns404()
    {
        (Organization organization, Account owner, _) = await SeedCallerAsync(SystemRoleKind.Owner);
        (_, _, IReadOnlyDictionary<SystemRoleKind, Role> otherRoles) = await SeedCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await SendAsync(
            ClientFor(owner, organization.Id),
            new { email = "b@example.com", roleId = otherRoles[SystemRoleKind.Member].Id });

        await AssertCodeAsync(response, HttpStatusCode.NotFound, EntityNotFoundCode);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
    }

    // Send: success

    [Fact]
    public async Task Send_Valid_Returns201AndMailsLink()
    {
        (Organization organization, Account owner, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Owner);
        Role member = roles[SystemRoleKind.Member];

        HttpResponseMessage response = await SendAsync(
            ClientFor(owner, organization.Id), new { email = " New@Example.com ", roleId = member.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement dto = await response.Content.ReadJsonElementAsync();
        OrganizationInvitation invitation = Assert.Single(await InvitationsOfAsync(organization.Id));
        Assert.Equal(invitation.Id, dto.GetProperty("id").GetGuid());
        Assert.Equal("new@example.com", dto.GetProperty("email").GetString());
        Assert.Equal(member.Id, dto.GetProperty("role").GetProperty("id").GetGuid());
        Assert.Equal("Member", dto.GetProperty("role").GetProperty("systemKind").GetString());
        Assert.Equal("Pending", dto.GetProperty("status").GetString());
        Assert.True(dto.GetProperty("canResend").GetBoolean());
        Assert.True(dto.GetProperty("canRevokeOrDelete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("lockReason").ValueKind);

        SentEmail email = Assert.Single(fixture.Emails.Sent);
        Assert.Equal("new@example.com", email.ToEmail);
        string token = InviteLink().Match(email.HtmlBody).Groups[1].Value;
        Assert.NotEmpty(token);
        Assert.Equal(Sha256(token), invitation.TokenHash);

        AuditLog recorded = Assert.Single(await EventsOfAsync(organization.Id));
        Assert.Equal(nameof(AuditEventType.InvitationSent), recorded.Action);
        Assert.Equal(owner.Id.ToString(), recorded.UserId);
        Assert.DoesNotContain(token, recorded.NewValues ?? string.Empty);
        Assert.DoesNotContain(invitation.TokenHash, recorded.NewValues ?? string.Empty);
    }

    // Review Focus 2 over HTTP: the rollback keeps the earlier invitation and its token, and records nothing.
    [Fact]
    public async Task Send_SmtpFails_Returns503AndKeepsEarlierInvitation()
    {
        (Organization organization, Account owner, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Owner);
        OrganizationInvitation earlier = await SeedInvitationAsync(
            organization.Id, "b@example.com", roles[SystemRoleKind.Member].Id, "earlier-token");
        fixture.Emails.FailNextSend();

        HttpResponseMessage response = await SendAsync(
            ClientFor(owner, organization.Id), new { email = "B@example.com", roleId = roles[SystemRoleKind.Admin].Id });

        await AssertCodeAsync(
            response, HttpStatusCode.ServiceUnavailable, ApplicationMessages.InvitationEmailDeliveryFailed);
        OrganizationInvitation kept = Assert.Single(await InvitationsOfAsync(organization.Id));
        Assert.Equal(earlier.Id, kept.Id);
        Assert.Equal(Sha256("earlier-token"), kept.TokenHash);
        Assert.Equal(roles[SystemRoleKind.Member].Id, kept.RoleId);
        Assert.Equal(earlier.ExpiresAt, kept.ExpiresAt, TimeSpan.FromMilliseconds(1));
        Assert.Empty(await EventsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }

    // AC-180
    [Fact]
    public async Task Send_EmailOfActiveMember_Returns409AlreadyMember()
    {
        (Organization organization, Account owner, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedCallerAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await SendAsync(
            ClientFor(owner, organization.Id),
            new { email = owner.Email.ToUpperInvariant(), roleId = roles[SystemRoleKind.Member].Id });

        await AssertCodeAsync(response, HttpStatusCode.Conflict, ApplicationMessages.InvitationEmailAlreadyMember);
        Assert.Empty(await InvitationsOfAsync(organization.Id));
        Assert.Empty(fixture.Emails.Sent);
    }
}
