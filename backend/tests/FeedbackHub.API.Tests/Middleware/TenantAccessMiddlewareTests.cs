using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Middleware;

[Collection(nameof(ApiCollection))]
public sealed class TenantAccessMiddlewareTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task OrganizationScopedToken_ActiveMembership_AllowsRequest()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-ok-{Guid.NewGuid():N}", email: $"tenant-ok-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, account.Id);
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_NoMembershipRow_ReturnsForbidden()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-revoked-{Guid.NewGuid():N}", email: $"tenant-revoked-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No OrganizationMember row seeded — the token claims a tenant the account no longer
            // (or never did) belong to, e.g. removed after the token was issued.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_RevokedAccess_AuthEndpointStillAllowed()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-logout-{Guid.NewGuid():N}", email: $"tenant-logout-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No membership seeded — access to this org is not (or no longer) valid, but logout
            // must still work so the client can clear its own session state.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_RevokedAccess_OrganizationsEndpointStillAllowed()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-list-{Guid.NewGuid():N}", email: $"tenant-list-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No membership seeded for `organization` — the token's active org is no longer
            // accessible, but /api/organizations (list-mine, switch targets) must stay reachable
            // so the client can discover other orgs it belongs to and recover.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(Account Account, Organization Organization)> SeedAsync(
        bool member, bool memberLeft = false, bool organizationSuspended = false)
    {
        await using AppDbContext context = CreateDbContext();
        Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
            context, username: $"tenant-{Guid.NewGuid():N}", email: $"tenant-{Guid.NewGuid():N}@example.com");
        Organization organization = await AuthTestHelper.SeedOrganizationAsync(context, name: "Suspended Co");

        if (member)
        {
            OrganizationMember membership =
                await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, account.Id);
            if (memberLeft)
            {
                membership.Deactivate();
                context.OrganizationMembers.Update(membership);
            }
        }

        if (organizationSuspended)
        {
            organization.Deactivate(new DeactivationParams(account.Id, false, null, DateTime.UtcNow));
            context.Organizations.Update(organization);
        }

        await context.SaveChangesAsync();

        return (account, organization);
    }

    private HttpClient CreateClientFor(Account account, Guid organizationId)
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organizationId));
        return client;
    }

    // AC-320, AC-183: the member of a suspended organization learns which one, beside the code.
    [Fact]
    public async Task TenantAccess_SuspendedOrganizationMember_Returns403SuspendedWithName()
    {
        (Account account, Organization organization) = await SeedAsync(member: true, organizationSuspended: true);
        HttpClient client = CreateClientFor(account, organization.Id);

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(403, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(organization.Name, body.RootElement.GetProperty("organizationName").GetString());
        Assert.False(body.RootElement.TryGetProperty("extensions", out _));
    }

    // AC-320: someone removed from the organization never learns its name or that it is suspended.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TenantAccess_RemovedMember_Returns403AccessDeniedWithoutName(bool organizationSuspended)
    {
        (Account account, Organization organization) =
            await SeedAsync(member: true, memberLeft: true, organizationSuspended: organizationSuspended);
        HttpClient client = CreateClientFor(account, organization.Id);

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        using JsonDocument body = JsonDocument.Parse(raw);
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("organizationName", out _));
        Assert.DoesNotContain(organization.Name, raw);
    }

    // Paths no phase maps: whatever answers them, it is not this gate.
    [Theory]
    [InlineData("/api/system/__probe")]
    [InlineData("/api/invitations/__probe/__probe")]
    public async Task TenantAccess_SystemAndInvitationsPaths_AreExempt(string path)
    {
        (Account account, Organization organization) = await SeedAsync(member: true, organizationSuspended: true);
        HttpClient client = CreateClientFor(account, organization.Id);

        HttpResponseMessage response = await client.GetAsync(path);

        string raw = await response.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(ApplicationMessages.OrganizationAccessDenied, raw);
        Assert.DoesNotContain(ApplicationMessages.OrganizationSuspended, raw);
    }

    // "/api/organizations" is exempt by segment, so the singular path stays gated.
    [Fact]
    public async Task TenantAccess_SingularOrganizationPath_IsNotExempt()
    {
        (Account account, Organization organization) = await SeedAsync(member: true, organizationSuspended: true);
        HttpClient client = CreateClientFor(account, organization.Id);

        HttpResponseMessage response = await client.GetAsync("/api/organization/__probe");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.RootElement.GetProperty("code").GetString());
    }
}
