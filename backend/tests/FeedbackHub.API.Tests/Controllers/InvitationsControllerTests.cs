using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Controllers;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class InvitationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string LookupPath = "/api/invitations/lookup";
    private const string AcceptPath = "/api/invitations/accept";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<Account> SeedAccountAsync(string email, bool confirmed = true)
    {
        await using AppDbContext context = CreateDbContext();
        string handle = $"invitee-{Guid.NewGuid():N}";
        if (confirmed)
        {
            return await AuthTestHelper.SeedConfirmedAccountAsync(context, username: handle, email: email);
        }

        Account account = Account.Create(new AccountParams("Chưa xác minh", handle, email));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    private async Task<(Organization Organization, IReadOnlyDictionary<SystemRoleKind, Role> Roles)>
        SeedOrganizationAsync(string name)
    {
        Account owner = await SeedAccountAsync($"owner-{Guid.NewGuid():N}@example.com");
        await using AppDbContext context = CreateDbContext();
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: name);
        await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, owner.Id);
        return (organization, await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id));
    }

    private async Task<OrganizationInvitation> SeedInvitationAsync(
        Guid organizationId, string email, Guid roleId, string rawToken)
    {
        await using AppDbContext context = CreateDbContext();
        return await AuthTestHelper.SeedInvitationAsync(context, organizationId, email, roleId, rawToken);
    }

    private async Task SuspendAsync(Organization organization)
    {
        await using AppDbContext context = CreateDbContext();
        Organization stored = await context.Organizations.SingleAsync(candidate => candidate.Id == organization.Id);
        stored.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        context.Organizations.Update(stored);
        await context.SaveChangesAsync();
    }

    private async Task<List<AuditLog>> EventsOfAsync(Guid organizationId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.AuditLogs
            .Where(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event)
            .ToListAsync();
    }

    private async Task<bool> IsActiveMemberAsync(Guid organizationId, Guid accountId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.OrganizationMembers.AnyAsync(member =>
            member.OrganizationId == organizationId && member.AccountId == accountId && member.IsActive);
    }

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

    // TECH00 §4.6: the link's answer is never cached, sent on as a referrer or indexed (F16).
    private static void AssertPrivateLinkHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore, "Cache-Control: no-store missing");
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("noindex", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }

    private static void AssertOnlyStatus(JsonElement lookup, string status)
    {
        Assert.Equal(status, lookup.GetProperty("status").GetString());
        foreach (string field in new[] { "organizationName", "role", "expiresAt", "email" })
        {
            Assert.Equal(JsonValueKind.Null, lookup.GetProperty(field).ValueKind);
        }
    }

    // Lookup

    // AC-309
    [Fact]
    public async Task Lookup_Anonymous_Returns200WithNoStoreNoReferrerNoindexHeaders()
    {
        (Organization organization, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedOrganizationAsync("Bảo Minh");
        OrganizationInvitation invitation = await SeedInvitationAsync(
            organization.Id, "k@example.com", roles[SystemRoleKind.Admin].Id, "lookup-token");

        HttpResponseMessage response =
            await fixture.CreateTestClient().PostAsJsonAsync(LookupPath, new { token = "lookup-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPrivateLinkHeaders(response);
        JsonElement lookup = await response.Content.ReadJsonElementAsync();
        Assert.Equal("Valid", lookup.GetProperty("status").GetString());
        Assert.Equal("Bảo Minh", lookup.GetProperty("organizationName").GetString());
        Assert.Equal(roles[SystemRoleKind.Admin].Id, lookup.GetProperty("role").GetProperty("id").GetGuid());
        Assert.Equal("k@example.com", lookup.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.String, lookup.GetProperty("expiresAt").ValueKind);
        Assert.Equal(invitation.Id, Assert.Single(await CreateDbContext().OrganizationInvitations.ToListAsync()).Id);
    }

    // D10: an unknown, used or revoked code says nothing more, and carries the same headers.
    [Fact]
    public async Task Lookup_UnknownToken_Returns200NoLongerValidWithPrivateLinkHeaders()
    {
        HttpResponseMessage response =
            await fixture.CreateTestClient().PostAsJsonAsync(LookupPath, new { token = "never-issued" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPrivateLinkHeaders(response);
        AssertOnlyStatus(await response.Content.ReadJsonElementAsync(), "NoLongerValid");
    }

    [Fact]
    public async Task Lookup_MissingToken_Returns400WithPrivateLinkHeaders()
    {
        HttpResponseMessage response = await fixture.CreateTestClient().PostAsJsonAsync(LookupPath, new { });

        await AssertCodeAsync(response, HttpStatusCode.BadRequest, ApplicationMessages.ValidationFailed);
        AssertPrivateLinkHeaders(response);
    }

    // AC-306, AC-308
    [Fact]
    public async Task Lookup_SignedInWrongAccount_ReturnsWrongAccountOnly()
    {
        (Organization organization, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedOrganizationAsync("Bảo Minh");
        await SeedInvitationAsync(organization.Id, "k@example.com", roles[SystemRoleKind.Member].Id, "wrong-token");
        Account other = await SeedAccountAsync("someone-else@example.com");

        HttpResponseMessage response =
            await ClientFor(other, null).PostAsJsonAsync(LookupPath, new { token = "wrong-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOnlyStatus(await response.Content.ReadJsonElementAsync(), "WrongAccount");
    }

    // F14/F19: an API key is no person, so the lookup answers as to an anonymous visitor (Valid even when suspended).
    [Fact]
    public async Task Lookup_ApiKey_AnswersAsAnonymous()
    {
        (Organization organization, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedOrganizationAsync("Bảo Minh");
        await SeedInvitationAsync(organization.Id, "k@example.com", roles[SystemRoleKind.Member].Id, "key-token");
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "lookup-key");
        }
        await SuspendAsync(organization);
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.PostAsJsonAsync(LookupPath, new { token = "key-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement lookup = await response.Content.ReadJsonElementAsync();
        Assert.Equal("Valid", lookup.GetProperty("status").GetString());
        Assert.Equal("k@example.com", lookup.GetProperty("email").GetString());
    }

    // TECH00 §11, F10: the shared host raises the limit, so this host pins it to one request a window.
    [Fact]
    public async Task Lookup_OverLimit_Returns429TooManyRequests()
    {
        await using WebApplicationFactory<Program> limited = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiterSettings:InvitationPermitLimit", "1"));
        HttpClient client = limited.CreateClient();
        client.DefaultRequestHeaders.Add("X-TimeZone", "UTC");

        HttpResponseMessage first = await client.PostAsJsonAsync(LookupPath, new { token = "limited-token" });
        HttpResponseMessage second = await client.PostAsJsonAsync(LookupPath, new { token = "limited-token" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertCodeAsync(second, HttpStatusCode.TooManyRequests, ApplicationMessages.TooManyRequests);
    }

    // Accept

    [Fact]
    public async Task Accept_Anonymous_Returns401()
    {
        HttpResponseMessage response =
            await fixture.CreateTestClient().PostAsJsonAsync(AcceptPath, new { token = "any-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // SignedInUser takes a person's JWT only (F14).
    [Fact]
    public async Task Accept_ApiKey_Returns401AndKeepsInvitation()
    {
        (Organization organization, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedOrganizationAsync("Bảo Minh");
        OrganizationInvitation invitation = await SeedInvitationAsync(
            organization.Id, "k@example.com", roles[SystemRoleKind.Member].Id, "accept-key-token");
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organization.Id, "accept-key");
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.PostAsJsonAsync(AcceptPath, new { token = "accept-key-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(invitation.Id, Assert.Single(await CreateDbContext().OrganizationInvitations.ToListAsync()).Id);
    }

    // AC-351
    [Fact]
    public async Task Accept_UnconfirmedAccount_Returns403EmailNotConfirmedInvitationKept()
    {
        (Organization organization, IReadOnlyDictionary<SystemRoleKind, Role> roles) =
            await SeedOrganizationAsync("Bảo Minh");
        OrganizationInvitation invitation = await SeedInvitationAsync(
            organization.Id, "k@example.com", roles[SystemRoleKind.Member].Id, "unconfirmed-token");
        Account invitee = await SeedAccountAsync("k@example.com", confirmed: false);

        HttpResponseMessage response =
            await ClientFor(invitee, null).PostAsJsonAsync(AcceptPath, new { token = "unconfirmed-token" });

        await AssertCodeAsync(response, HttpStatusCode.Forbidden, ApplicationMessages.EmailNotConfirmed);
        Assert.Equal(invitation.Id, Assert.Single(await CreateDbContext().OrganizationInvitations.ToListAsync()).Id);
        Assert.False(await IsActiveMemberAsync(organization.Id, invitee.Id));
        Assert.Empty(await EventsOfAsync(organization.Id));
    }

    // A1: K works in B and accepts an invitation into A — the entry is A's, whatever org_id the session carries.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Accept_FromOtherOrganizationOrPersonalContext_MemberJoinedRecordedInInvitedOrganization(
        bool fromOtherOrganization)
    {
        (Organization invited, IReadOnlyDictionary<SystemRoleKind, Role> roles) = await SeedOrganizationAsync("A");
        (Organization working, _) = await SeedOrganizationAsync("B");
        Account invitee = await SeedAccountAsync("k@example.com");
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedOrganizationMemberAsync(context, working.Id, invitee.Id, SystemRoleKind.Member);
        }
        await SeedInvitationAsync(invited.Id, "k@example.com", roles[SystemRoleKind.Member].Id, "join-token");

        HttpResponseMessage response = await ClientFor(invitee, fromOtherOrganization ? working.Id : null)
            .PostAsJsonAsync(AcceptPath, new { token = "join-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement accepted = await response.Content.ReadJsonElementAsync();
        Assert.Equal(invited.Id, accepted.GetProperty("organizationId").GetGuid());
        Assert.Equal("A", accepted.GetProperty("organizationName").GetString());
        Assert.True(await IsActiveMemberAsync(invited.Id, invitee.Id));
        AuditLog joined = Assert.Single(await EventsOfAsync(invited.Id));
        Assert.Equal(nameof(AuditEventType.MemberJoined), joined.Action);
        Assert.Equal(invitee.Id.ToString(), joined.UserId);
        Assert.Empty(await EventsOfAsync(working.Id));
        Assert.Empty(await CreateDbContext().OrganizationInvitations.ToListAsync());
    }

    // Attributes

    // Anonymous surface of InvitationsController: adding a public action must be a deliberate edit of this list
    // (and of the list in .claude/rules/authentication.md).
    [Fact]
    public void InvitationsController_AnonymousActions_MatchTheDeliberateList()
    {
        Type controller = typeof(InvitationsController);
        string[] anonymous = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Length > 0)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(["Lookup"], anonymous);
        Assert.Empty(controller.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        AuthorizeAttribute authorize = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>(true));
        Assert.Equal(AuthorizationPolicies.SignedInUser, authorize.Policy);
    }

    // TECH00 §4.8: lookup and accept share the "invitation" bucket; managing invitations has no limit of its own.
    [Fact]
    public void RateLimitedActions_UseInvitationPolicy()
    {
        static string? Policy(MethodInfo method) =>
            method.GetCustomAttribute<EnableRateLimitingAttribute>(true)?.PolicyName;

        MethodInfo[] actions = typeof(InvitationsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal(["Accept", "Lookup"], actions.Select(action => action.Name).Order());
        Assert.All(actions, action => Assert.Equal("invitation", Policy(action)));
        Assert.All(
            typeof(CurrentOrganizationController).GetMethods(BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.DeclaredOnly),
            action => Assert.Null(Policy(action)));
    }
}
