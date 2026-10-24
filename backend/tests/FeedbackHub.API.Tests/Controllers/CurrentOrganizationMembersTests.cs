using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CurrentOrganizationMembersTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Path = "/api/organization/members";

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

    private async Task SeedMemberAsync(Guid organizationId, Guid accountId, SystemRoleKind role)
    {
        await using AppDbContext context = CreateDbContext();
        await AuthTestHelper.SeedOrganizationMemberAsync(context, organizationId, accountId, role);
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
}
