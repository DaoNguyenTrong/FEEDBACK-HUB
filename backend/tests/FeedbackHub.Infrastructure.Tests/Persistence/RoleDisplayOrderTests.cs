using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class RoleDisplayOrderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private Organization organization = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
    }

    // Never committed: disposing the transaction rolls every seeded row back, the dropped index included.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    // Inserted one by one in the order given, so the heap order is that order.
    private async Task InsertAsync(IEnumerable<Role> roles)
    {
        foreach (Role role in roles)
        {
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }
    }

    private Task<List<Guid>> OrderedIdsAsync() =>
        context.Roles
            .Where(role => role.OrganizationId == organization.Id)
            .OrderForDisplay()
            .Select(role => role.Id)
            .ToListAsync();

    // OrderForDisplay

    // Code-point order would give "Bảo Minh", "an Bình", "Âu Cơ" (AC-302 order, reused for roles).
    [Fact]
    public async Task OrderForDisplay_MixedRoles_SystemKindsThenCustomByIcuName()
    {
        Role bao = Role.Create(new RoleParams(organization.Id, "Bảo Minh"));
        Role member = Role.Create(new RoleParams(organization.Id, "Member", SystemRoleKind.Member));
        Role an = Role.Create(new RoleParams(organization.Id, "an Bình"));
        Role admin = Role.Create(new RoleParams(organization.Id, "Admin", SystemRoleKind.Admin));
        Role au = Role.Create(new RoleParams(organization.Id, "Âu Cơ"));
        Role owner = Role.Create(new RoleParams(organization.Id, "Owner", SystemRoleKind.Owner));
        await InsertAsync([bao, member, an, admin, au, owner]);

        List<Guid> ids = await OrderedIdsAsync();

        Assert.Equal([owner.Id, admin.Id, member.Id, an.Id, au.Id, bao.Id], ids);
    }

    // Equal names cannot coexist under the unique index, so it is dropped inside this rolled-back transaction.
    [Fact]
    public async Task OrderForDisplay_EqualNames_OrderedById()
    {
        await context.Database.ExecuteSqlRawAsync($"""DROP INDEX "{UnitOfWork.RoleNameUniqueIndexName}" """);
        Role[] sameName =
        [
            Role.Create(new RoleParams(organization.Id, "Kiểm toán")),
            Role.Create(new RoleParams(organization.Id, "Kiểm toán"))
        ];

        // PostgreSQL compares uuid bytes, which matches the ordinal order of the hex text, not Guid.CompareTo.
        Guid[] expected = sameName.Select(role => role.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        await InsertAsync(expected.Reverse().Select(id => sameName.Single(role => role.Id == id)));

        List<Guid> ids = await OrderedIdsAsync();

        Assert.Equal(expected, ids);
    }
}
