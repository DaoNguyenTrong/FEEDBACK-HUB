using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class OrganizationMemberQueriesTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private OrganizationMemberQueries queries = null!;
    private Organization organization = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        queries = new OrganizationMemberQueries(context);
        organization = await SeedOrganizationAsync();
    }

    // Never committed: disposing the transaction rolls every seeded row back.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<Organization> SeedOrganizationAsync()
    {
        Organization seeded = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(seeded);
        await context.SaveChangesAsync();
        return seeded;
    }

    private async Task<OrganizationMember> SeedMemberAsync(
        string name, Guid? organizationId = null, bool active = true)
    {
        string handle = $"member-{Guid.NewGuid():N}";
        Account account = Account.Create(new AccountParams(name, handle, $"{handle}@example.com"));
        OrganizationMember member =
            OrganizationMember.Create(new OrganizationMemberParams(organizationId ?? organization.Id, account.Id));
        if (!active)
        {
            member.Deactivate();
        }

        context.Accounts.Add(account);
        context.OrganizationMembers.Add(member);
        await context.SaveChangesAsync();
        return member;
    }

    private Task<PagedResult<MemberRow>> ListAsync(int pageNumber = 1, int pageSize = 50) =>
        queries.ListActiveAsync(organization.Id, new PageRequest(pageNumber, pageSize), CancellationToken.None);

    [Fact]
    public async Task ListActiveAsync_InactiveAndForeignMembers_AreExcluded()
    {
        OrganizationMember active = await SeedMemberAsync("An");
        await SeedMemberAsync("Bình", active: false);
        Organization other = await SeedOrganizationAsync();
        await SeedMemberAsync("Cúc", other.Id);

        PagedResult<MemberRow> result = await ListAsync();

        MemberRow row = Assert.Single(result.Items);
        Assert.Equal(active.Id, row.MemberId);
        Assert.Equal(active.AccountId, row.AccountId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ListActiveAsync_ReturnsAccountNameAndEmail()
    {
        OrganizationMember member = await SeedMemberAsync("Nguyễn An");
        Account account = await context.Accounts.FindAsync(member.AccountId) ?? throw new InvalidOperationException();

        MemberRow row = Assert.Single((await ListAsync()).Items);

        Assert.Equal(("Nguyễn An", account.Email), (row.AccountName, row.Email));
    }

    // AC-302 order: ICU root collation, where the database default would sort by code point.
    [Fact]
    public async Task ListActiveAsync_Names_AreSortedByIcuCollation()
    {
        foreach (string name in new[] { "Cát Tường", "an Bình", "Bảo Minh", "Âu Cơ" })
        {
            await SeedMemberAsync(name);
        }

        PagedResult<MemberRow> result = await ListAsync();

        Assert.Equal(["an Bình", "Âu Cơ", "Bảo Minh", "Cát Tường"], result.Items.Select(item => item.AccountName));
    }

    [Fact]
    public async Task ListActiveAsync_SameName_IsSortedByAccountId()
    {
        OrganizationMember first = await SeedMemberAsync("Trùng Tên");
        OrganizationMember second = await SeedMemberAsync("Trùng Tên");

        PagedResult<MemberRow> result = await ListAsync();

        Assert.Equal(
            new[] { first.AccountId, second.AccountId }.OrderBy(id => id.ToString(), StringComparer.Ordinal),
            result.Items.Select(item => item.AccountId));
    }

    [Fact]
    public async Task ListActiveAsync_SecondPage_ReturnsTheRestWithTotalAndPaging()
    {
        foreach (string name in new[] { "A", "B", "C", "D", "E" })
        {
            await SeedMemberAsync(name);
        }

        PagedResult<MemberRow> result = await ListAsync(pageNumber: 2, pageSize: 2);

        Assert.Equal(["C", "D"], result.Items.Select(item => item.AccountName));
        Assert.Equal((5, 2, 2), (result.TotalCount, result.PageNumber, result.PageSize));
    }
}
