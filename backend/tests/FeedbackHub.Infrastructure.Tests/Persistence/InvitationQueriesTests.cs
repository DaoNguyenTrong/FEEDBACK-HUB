using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class InvitationQueriesTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private InvitationQueries queries = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        queries = new InvitationQueries(context);
    }

    // Never committed: disposing the transaction rolls every seeded row back.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<(Guid OrganizationId, Role Role)> SeedOrganizationAsync()
    {
        Organization organization = Organization.Create(new OrganizationParams("Lời mời", $"inv-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        Role role = Role.Create(new RoleParams(organization.Id, "Tester"));
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return (organization.Id, role);
    }

    private async Task<OrganizationInvitation> SeedInvitationAsync(
        Guid organizationId, Role role, string email, DateTime issuedAt)
    {
        string hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        OrganizationInvitation invitation = OrganizationInvitation.Create(
            new OrganizationInvitationParams(organizationId, email, role.Id, hash, issuedAt));
        context.OrganizationInvitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation;
    }

    // PostgreSQL orders uuid by its bytes, which is the ordinal order of the canonical lower-case text.
    private static int CompareAsPostgres(Guid left, Guid right) =>
        string.CompareOrdinal(left.ToString(), right.ToString());

    // TECH00 §4.8: newest expiry first, then id; pending and expired invitations alike.
    [Fact]
    public async Task ListAsync_OrdersByExpiresAtDescThenIdDesc()
    {
        (Guid organizationId, Role role) = await SeedOrganizationAsync();
        OrganizationInvitation expired = await SeedInvitationAsync(organizationId, role, "old@x.com", Now.AddDays(-9));
        OrganizationInvitation tieOne = await SeedInvitationAsync(organizationId, role, "tie1@x.com", Now.AddDays(-2));
        OrganizationInvitation newest = await SeedInvitationAsync(organizationId, role, "new@x.com", Now);
        OrganizationInvitation tieTwo = await SeedInvitationAsync(organizationId, role, "tie2@x.com", Now.AddDays(-2));
        Guid[] tie = [tieOne.Id, tieTwo.Id];
        Array.Sort(tie, (left, right) => CompareAsPostgres(right, left));

        PagedResult<InvitationRow> page =
            await queries.ListAsync(organizationId, new PageRequest(), CancellationToken.None);

        Assert.Equal([newest.Id, tie[0], tie[1], expired.Id], page.Items.Select(row => row.Id));
        InvitationRow first = page.Items[0];
        Assert.Equal("new@x.com", first.Email);
        Assert.Equal(role.Id, first.RoleId);
        Assert.Equal(newest.ExpiresAt, first.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, first.ExpiresAt.Kind);
    }

    // AC-310: another organization's invitations never appear.
    [Fact]
    public async Task ListAsync_OnlyCurrentOrganization()
    {
        (Guid mineId, Role mine) = await SeedOrganizationAsync();
        (Guid otherId, Role other) = await SeedOrganizationAsync();
        OrganizationInvitation kept = await SeedInvitationAsync(mineId, mine, "a@x.com", Now);
        await SeedInvitationAsync(otherId, other, "a@x.com", Now);
        await SeedInvitationAsync(otherId, other, "b@x.com", Now);

        PagedResult<InvitationRow> page = await queries.ListAsync(mineId, new PageRequest(), CancellationToken.None);

        Assert.Equal([kept.Id], page.Items.Select(row => row.Id));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_SecondPage_SkipsFirstPageAndCountsAll()
    {
        (Guid organizationId, Role role) = await SeedOrganizationAsync();
        List<OrganizationInvitation> seeded = [];
        for (int day = 0; day < 5; day++)
        {
            seeded.Add(await SeedInvitationAsync(organizationId, role, $"p{day}@x.com", Now.AddDays(-day)));
        }

        PagedResult<InvitationRow> page =
            await queries.ListAsync(organizationId, new PageRequest(2, 2), CancellationToken.None);

        Assert.Equal([seeded[2].Id, seeded[3].Id], page.Items.Select(row => row.Id));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
    }

    [Fact]
    public async Task ListAsync_NoInvitations_ReturnsEmptyPage()
    {
        (Guid organizationId, _) = await SeedOrganizationAsync();

        PagedResult<InvitationRow> page =
            await queries.ListAsync(organizationId, new PageRequest(), CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }
}
