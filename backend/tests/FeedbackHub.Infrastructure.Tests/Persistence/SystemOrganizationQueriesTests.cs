using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class SystemOrganizationQueriesTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private SystemOrganizationQueries queries = null!;

    // The list covers every organization, so each test starts from an empty table; the rollback restores it.
    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE organizations CASCADE");
        queries = new SystemOrganizationQueries(context);
    }

    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<Organization> SeedOrganizationAsync(string name, bool deactivated = false)
    {
        Organization organization = Organization.Create(new OrganizationParams(name, $"org-{Guid.NewGuid():N}"));
        if (deactivated)
        {
            organization.Deactivate(new DeactivationParams(Guid.NewGuid(), true, "Hết hợp đồng", DateTime.UtcNow));
        }

        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    private async Task SeedMemberAsync(Guid organizationId, bool active = true)
    {
        string handle = $"member-{Guid.NewGuid():N}";
        Account account = Account.Create(new AccountParams("Member", handle, $"{handle}@example.com"));
        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, account.Id));
        if (!active)
        {
            member.Deactivate();
        }

        context.Accounts.Add(account);
        context.OrganizationMembers.Add(member);
        await context.SaveChangesAsync();
    }

    private Task<PagedResult<SystemOrganizationDto>> ListAsync(int pageNumber = 1, int pageSize = 50) =>
        queries.ListAsync(new PageRequest(pageNumber, pageSize), CancellationToken.None);

    // ListAsync

    // AC-302: Vietnamese names sort as a reader expects, whatever their case and accents.
    [Fact]
    public async Task ListAsync_OrdersByNameIgnoringCaseAndAccents()
    {
        await SeedOrganizationAsync("Cát Tường");
        await SeedOrganizationAsync("an Bình");
        await SeedOrganizationAsync("Bảo Minh");
        await SeedOrganizationAsync("Âu Cơ");

        PagedResult<SystemOrganizationDto> result = await ListAsync();

        Assert.Equal(["an Bình", "Âu Cơ", "Bảo Minh", "Cát Tường"], result.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task ListAsync_SameName_OrdersById()
    {
        Organization first = await SeedOrganizationAsync("Trùng Tên");
        Organization second = await SeedOrganizationAsync("Trùng Tên");

        PagedResult<SystemOrganizationDto> result = await ListAsync();

        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal),
            result.Items.Select(item => item.Id));
    }

    // AC-125
    [Fact]
    public async Task ListAsync_MemberCount_CountsOnlyActiveMembers()
    {
        Organization crowded = await SeedOrganizationAsync("An Bình");
        Organization empty = await SeedOrganizationAsync("Bảo Minh");
        await SeedMemberAsync(crowded.Id);
        await SeedMemberAsync(crowded.Id);
        await SeedMemberAsync(crowded.Id, active: false);
        await SeedMemberAsync(empty.Id, active: false);

        PagedResult<SystemOrganizationDto> result = await ListAsync();

        Assert.Equal(2, result.Items.Single(item => item.Id == crowded.Id).MemberCount);
        Assert.Equal(0, result.Items.Single(item => item.Id == empty.Id).MemberCount);
    }

    // AC-124
    [Fact]
    public async Task ListAsync_IncludesDeactivatedWithFlag()
    {
        Organization active = await SeedOrganizationAsync("An Bình");
        Organization deactivated = await SeedOrganizationAsync("Bảo Minh", deactivated: true);

        PagedResult<SystemOrganizationDto> result = await ListAsync();

        Assert.Equal(
            [new SystemOrganizationDto(active.Id, "An Bình", 0, false),
             new SystemOrganizationDto(deactivated.Id, "Bảo Minh", 0, true)],
            result.Items);
    }

    // AC-127
    [Fact]
    public async Task ListAsync_Empty_ReturnsNoItems()
    {
        PagedResult<SystemOrganizationDto> result = await ListAsync();

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task ListAsync_SecondPage_ReturnsTheRestWithTotalAndPaging()
    {
        foreach (string name in new[] { "A", "B", "C", "D", "E" })
        {
            await SeedOrganizationAsync(name);
        }

        PagedResult<SystemOrganizationDto> result = await ListAsync(pageNumber: 2, pageSize: 2);

        Assert.Equal(["C", "D"], result.Items.Select(item => item.Name));
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }
}
