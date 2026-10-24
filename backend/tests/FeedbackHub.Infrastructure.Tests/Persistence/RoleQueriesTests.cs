using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class RoleQueriesTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private RoleQueries queries = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        queries = new RoleQueries(context);
    }

    // Never committed: disposing the transaction rolls every seeded row back.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<(Guid OrganizationId, IReadOnlyDictionary<SystemRoleKind, Role> System)> SeedOrganizationAsync()
    {
        Organization organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);

        Dictionary<SystemRoleKind, Role> roles = [];
        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organization.Id, kind.ToString(), kind));
            context.Roles.Add(role);
            roles[kind] = role;
        }

        context.RolePermissions.Add(RolePermission.Create(
            new RolePermissionParams(roles[SystemRoleKind.Admin].Id, Permissions.OrganizationMembersManage)));

        await context.SaveChangesAsync();
        return (organization.Id, roles);
    }

    private async Task<Role> SeedCustomRoleAsync(Guid organizationId, string name, params string[] permissionCodes)
    {
        Role role = Role.Create(new RoleParams(organizationId, name));
        context.Roles.Add(role);
        foreach (string code in permissionCodes)
        {
            context.RolePermissions.Add(RolePermission.Create(new RolePermissionParams(role.Id, code)));
        }

        await context.SaveChangesAsync();
        return role;
    }

    private async Task SeedMemberAsync(Guid organizationId, Role role, bool active)
    {
        string handle = $"member-{Guid.NewGuid():N}";
        Account account = Account.Create(new AccountParams("Member", handle, $"{handle}@example.com"));
        context.Accounts.Add(account);

        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, account.Id));
        if (!active)
        {
            member.Deactivate();
        }

        context.OrganizationMembers.Add(member);
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id)));
        await context.SaveChangesAsync();
    }

    // AC-252
    [Fact]
    public async Task ListAsync_HolderCountIgnoresInactiveMembers()
    {
        (Guid organizationId, IReadOnlyDictionary<SystemRoleKind, Role> system) = await SeedOrganizationAsync();
        Role custom = await SeedCustomRoleAsync(organizationId, "Support");
        await SeedMemberAsync(organizationId, custom, active: true);
        await SeedMemberAsync(organizationId, custom, active: true);
        await SeedMemberAsync(organizationId, custom, active: false);
        await SeedMemberAsync(organizationId, system[SystemRoleKind.Member], active: false);

        IReadOnlyList<RoleRow> rows = await queries.ListAsync(organizationId, CancellationToken.None);

        Assert.Equal(2, rows.Single(row => row.Id == custom.Id).HolderCount);
        Assert.Equal(0, rows.Single(row => row.Id == system[SystemRoleKind.Member].Id).HolderCount);
        Assert.Equal(0, rows.Single(row => row.Id == system[SystemRoleKind.Owner].Id).HolderCount);
    }

    [Fact]
    public async Task ListAsync_HolderCountsAreScopedToOrganization()
    {
        (Guid mineId, _) = await SeedOrganizationAsync();
        (Guid otherId, IReadOnlyDictionary<SystemRoleKind, Role> otherSystem) = await SeedOrganizationAsync();
        await SeedMemberAsync(otherId, otherSystem[SystemRoleKind.Member], active: true);

        IReadOnlyList<RoleRow> rows = await queries.ListAsync(mineId, CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(0, row.HolderCount));
    }

    [Fact]
    public async Task ListAsync_SystemRolesFirstThenCustomByName()
    {
        (Guid organizationId, IReadOnlyDictionary<SystemRoleKind, Role> system) = await SeedOrganizationAsync();
        Role bao = await SeedCustomRoleAsync(organizationId, "Bảo Minh");
        Role au = await SeedCustomRoleAsync(organizationId, "Âu Cơ");
        Role an = await SeedCustomRoleAsync(organizationId, "an Bình");

        IReadOnlyList<RoleRow> rows = await queries.ListAsync(organizationId, CancellationToken.None);

        Assert.Equal(
            [
                system[SystemRoleKind.Owner].Id,
                system[SystemRoleKind.Admin].Id,
                system[SystemRoleKind.Member].Id,
                an.Id,
                au.Id,
                bao.Id
            ],
            rows.Select(row => row.Id));
    }

    [Fact]
    public async Task ListAsync_OwnerHasAllPermissions()
    {
        (Guid organizationId, IReadOnlyDictionary<SystemRoleKind, Role> system) = await SeedOrganizationAsync();
        Role custom = await SeedCustomRoleAsync(organizationId, "Support", Permissions.OrganizationRolesManage);

        IReadOnlyList<RoleRow> rows = await queries.ListAsync(organizationId, CancellationToken.None);

        RoleRow owner = rows.Single(row => row.Id == system[SystemRoleKind.Owner].Id);
        Assert.Equal(SystemRoleKind.Owner, owner.SystemKind);
        Assert.Equal(Permissions.All.ToHashSet(), owner.PermissionCodes.ToHashSet());
        Assert.Equal(
            [Permissions.OrganizationMembersManage],
            rows.Single(row => row.Id == system[SystemRoleKind.Admin].Id).PermissionCodes);
        Assert.Equal([Permissions.OrganizationRolesManage], rows.Single(row => row.Id == custom.Id).PermissionCodes);
    }

    [Fact]
    public async Task GetAsync_RoleOfOrganization_ReturnsRowWithHolderCount()
    {
        (Guid organizationId, _) = await SeedOrganizationAsync();
        Role custom = await SeedCustomRoleAsync(organizationId, "Support", Permissions.OrganizationRolesManage);
        await SeedMemberAsync(organizationId, custom, active: true);
        await SeedMemberAsync(organizationId, custom, active: false);

        RoleRow? row = await queries.GetAsync(organizationId, custom.Id, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal("Support", row.Name);
        Assert.Null(row.SystemKind);
        Assert.Equal([Permissions.OrganizationRolesManage], row.PermissionCodes);
        Assert.Equal(1, row.HolderCount);
    }

    [Fact]
    public async Task GetAsync_RoleOfAnotherOrganizationOrUnknown_ReturnsNull()
    {
        (Guid mineId, _) = await SeedOrganizationAsync();
        (Guid otherId, _) = await SeedOrganizationAsync();
        Role elsewhere = await SeedCustomRoleAsync(otherId, "Support");

        Assert.Null(await queries.GetAsync(mineId, elsewhere.Id, CancellationToken.None));
        Assert.Null(await queries.GetAsync(mineId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_OwnerRole_HasAllPermissions()
    {
        (Guid organizationId, IReadOnlyDictionary<SystemRoleKind, Role> system) = await SeedOrganizationAsync();

        RoleRow? row = await queries.GetAsync(organizationId, system[SystemRoleKind.Owner].Id, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(Permissions.All.ToHashSet(), row.PermissionCodes.ToHashSet());
    }

    // IsNameTakenAsync

    // AC-290, AC-326: case is ignored for non-ASCII letters too.
    [Theory]
    [InlineData("tester")]
    [InlineData("TESTER")]
    [InlineData("ước lượng")]
    public async Task IsNameTakenAsync_SameNameInAnotherCase_ReturnsTrue(string name)
    {
        (Guid organizationId, _) = await SeedOrganizationAsync();
        await SeedCustomRoleAsync(organizationId, "Tester");
        await SeedCustomRoleAsync(organizationId, "Ước Lượng");

        Assert.True(await queries.IsNameTakenAsync(organizationId, name, null, CancellationToken.None));
    }

    [Fact]
    public async Task IsNameTakenAsync_OnlyTheExcludedRoleHasIt_ReturnsFalse()
    {
        (Guid organizationId, _) = await SeedOrganizationAsync();
        Role tester = await SeedCustomRoleAsync(organizationId, "Tester");

        Assert.False(await queries.IsNameTakenAsync(organizationId, "TESTER", tester.Id, CancellationToken.None));
        Assert.False(await queries.IsNameTakenAsync(organizationId, "Kiểm thử", null, CancellationToken.None));
    }

    // AC-292
    [Fact]
    public async Task IsNameTakenAsync_NameInAnotherOrganization_ReturnsFalse()
    {
        (Guid mineId, _) = await SeedOrganizationAsync();
        (Guid otherId, _) = await SeedOrganizationAsync();
        await SeedCustomRoleAsync(otherId, "Tester");

        Assert.False(await queries.IsNameTakenAsync(mineId, "Tester", null, CancellationToken.None));
    }

    // F3: the check must use the unique index's expression, or it and the index could disagree on what is equal.
    [Fact]
    public async Task IsNameTakenAsync_Sql_UsesTheUniqueIndexExpression()
    {
        List<string> commands = [];
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted])
            .Options;
        await using AppDbContext logged = new(options);

        await new RoleQueries(logged).IsNameTakenAsync(Guid.NewGuid(), "Tester", Guid.NewGuid(), CancellationToken.None);

        string sql = Assert.Single(commands);
        Assert.Contains("lower(r.\"Name\" COLLATE \"und-x-icu\")", sql);
        Assert.Contains("lower(@name COLLATE \"und-x-icu\")", sql);
    }

    // lower() follows the database ctype: under a C-locale database it would keep "Ước" and "ƯỚC" apart. The query
    // is upper case so that both sides of the comparison must carry the ICU collation.
    [Fact]
    public async Task IsNameTakenAsync_OnCLocaleDatabase_IgnoresNonAsciiCase()
    {
        string database = $"c_locale_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = new($"CREATE DATABASE {database} TEMPLATE template0 LOCALE 'C'", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            NpgsqlConnectionStringBuilder builder = new(fixture.ConnectionString) { Database = database, Pooling = false };
            Guid organizationId = Guid.NewGuid();
            await using (NpgsqlConnection cLocale = new(builder.ConnectionString))
            {
                await cLocale.OpenAsync();
                await using NpgsqlCommand setup = new("""
                    CREATE TABLE roles ("Id" uuid PRIMARY KEY, "OrganizationId" uuid NOT NULL, "Name" varchar(100) NOT NULL);
                    INSERT INTO roles VALUES (gen_random_uuid(), @organization, 'Ước');
                    """, cLocale);
                setup.Parameters.AddWithValue("organization", organizationId);
                await setup.ExecuteNonQueryAsync();
            }

            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options;
            await using AppDbContext cLocaleContext = new(options);

            Assert.True(await new RoleQueries(cLocaleContext)
                .IsNameTakenAsync(organizationId, "ƯỚC", null, CancellationToken.None));
        }
        finally
        {
            await using NpgsqlConnection admin = new(fixture.ConnectionString);
            await admin.OpenAsync();
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
