using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Migrations;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence.Migrations;

// The fixture database is already migrated through M3, so each M3 test first restores the pre-M3 index inside a
// transaction that is rolled back. Rows are seeded with raw SQL because Role.Create already trims and normalizes.
[Collection(nameof(PostgresCollection))]
public sealed class RoleMigrationsTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string OldIndexSql = """
        DROP INDEX "IX_roles_OrganizationId_lower_Name";
        CREATE UNIQUE INDEX "IX_roles_OrganizationId_Name" ON roles ("OrganizationId", "Name");
        """;

    private AppDbContext context = null!;
    private NpgsqlConnection connection = null!;
    private readonly List<string> notices = [];
    private DateTime clock = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        await context.Database.BeginTransactionAsync();
        connection = (NpgsqlConnection)context.Database.GetDbConnection();
        connection.Notice += (_, args) => notices.Add(args.Notice.MessageText);
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await context.Database.CurrentTransaction!.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task RestorePreM3IndexAsync() => await context.Database.ExecuteSqlRawAsync(OldIndexSql);

    private async Task<Guid> SeedOrganizationAsync()
    {
        Organization organization = Organization.Create(new OrganizationParams("Org", $"org-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization.Id;
    }

    private async Task<Guid> SeedRoleAsync(Guid organizationId, string name, SystemRoleKind? kind = null)
    {
        Guid id = Guid.NewGuid();
        clock = clock.AddMinutes(1);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO roles ("Id", "OrganizationId", "Name", "SystemRoleKind", "CreatedAt")
            VALUES ({id}, {organizationId}, {name}, {kind?.ToString()}, {clock})
            """);
        return id;
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(Guid organizationId)
    {
        List<(Guid Id, string Name)> rows = [];
        await using NpgsqlCommand command = new(
            """SELECT "Id", "Name" FROM roles WHERE "OrganizationId" = @org""", connection,
            (NpgsqlTransaction)context.Database.CurrentTransaction!.GetDbTransaction());
        command.Parameters.AddWithValue("org", organizationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetString(1)));
        }

        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    [Fact]
    public async Task M3_DuplicatesIgnoringCaseAndNfc_RenamedWithSuffix()
    {
        await RestorePreM3IndexAsync();
        Guid orgX = await SeedOrganizationAsync();
        Guid orgY = await SeedOrganizationAsync();
        Guid first = await SeedRoleAsync(orgX, "Tester");
        Guid second = await SeedRoleAsync(orgX, "tester");
        Guid third = await SeedRoleAsync(orgX, "TESTER");
        Guid otherOrg = await SeedRoleAsync(orgY, "Tester");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        Dictionary<Guid, string> names = await NamesAsync(orgX);
        Assert.Equal("Tester", names[first]);
        Assert.Equal("tester (2)", names[second]);
        Assert.Equal("TESTER (3)", names[third]);
        Assert.Equal("Tester", (await NamesAsync(orgY))[otherOrg]);
    }

    // Pins NFC and btrim in the step-2 key: the pairs differ only by normalization form or by a trailing space.
    [Fact]
    public async Task M3_NfcAndTrailingSpacePairs_LaterOneRenamed_IndexCreated()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        string composed = "Ước";
        Guid nfcFirst = await SeedRoleAsync(org, composed);
        Guid nfcSecond = await SeedRoleAsync(org, composed.Normalize(NormalizationForm.FormD));
        Guid trimFirst = await SeedRoleAsync(org, "Tester");
        Guid trimSecond = await SeedRoleAsync(org, "Tester ");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        Dictionary<Guid, string> names = await NamesAsync(org);
        Assert.Equal(composed, names[nfcFirst]);
        Assert.Equal(composed + " (2)", names[nfcSecond]);
        Assert.Equal("Tester", names[trimFirst]);
        Assert.Equal("Tester (2)", names[trimSecond]);
        Assert.Equal(1, await context.Database.SqlQuery<int>(
            $"""SELECT count(*)::int AS "Value" FROM pg_indexes WHERE indexname = 'IX_roles_OrganizationId_lower_Name'""")
            .SingleAsync());
    }

    [Fact]
    public async Task M3_CustomRoleNamedLikeSystemRole_Renamed()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        Guid owner = await SeedRoleAsync(org, "Owner", SystemRoleKind.Owner);
        Guid customOwner = await SeedRoleAsync(org, "owner");
        Guid customNfd = await SeedRoleAsync(org, "Chủ sở hữu");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        Dictionary<Guid, string> names = await NamesAsync(org);
        Assert.Equal("Owner", names[owner]);
        Assert.Equal("owner (2)", names[customOwner]);
        Assert.Equal("Chủ sở hữu (2)", names[customNfd]);
    }

    [Fact]
    public async Task M3_SuffixSkipsTakenName()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        await SeedRoleAsync(org, "Tester");
        Guid duplicate = await SeedRoleAsync(org, "tester");
        await SeedRoleAsync(org, "Tester (2)");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        Assert.Equal("tester (3)", (await NamesAsync(org))[duplicate]);
    }

    [Fact]
    public async Task M3_NameAt100Chars_SuffixFitsIn100()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        string name = new('a', 100);
        await SeedRoleAsync(org, name);
        Guid duplicate = await SeedRoleAsync(org, name.ToUpperInvariant());

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        string renamed = (await NamesAsync(org))[duplicate];
        Assert.Equal(100, renamed.Length);
        Assert.EndsWith(" (2)", renamed);
    }

    [Fact]
    public async Task M3_MultibyteNameAt100Chars_SuffixFitsIn100()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        string name = new('ư', 100);
        await SeedRoleAsync(org, name);
        Guid duplicate = await SeedRoleAsync(org, name.ToUpperInvariant());

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        string renamed = (await NamesAsync(org))[duplicate];
        Assert.Equal(100, renamed.Length);
        Assert.EndsWith(" (2)", renamed);
    }

    [Fact]
    public async Task M3_Notice_ContainsIdsNotNames()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        await SeedRoleAsync(org, "Secretname");
        Guid duplicate = await SeedRoleAsync(org, "SECRETNAME");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        string notice = Assert.Single(notices, n => n.StartsWith("M3") && n.Contains(org.ToString()));
        Assert.Contains(duplicate.ToString(), notice);
        Assert.Contains(org.ToString(), notice);
        Assert.DoesNotContain("secretname", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task M3_AfterRun_LowerNameIndexRejectsCaseDuplicate()
    {
        await RestorePreM3IndexAsync();
        Guid org = await SeedOrganizationAsync();
        await SeedRoleAsync(org, "  Padded  ");

        await context.Database.ExecuteSqlRawAsync(RenameDuplicateRoleNames.UpSql);

        Assert.Equal("Padded", (await NamesAsync(org)).Values.Single());
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(
            () => SeedRoleAsync(org, "PADDED"));
        Assert.Equal("IX_roles_OrganizationId_lower_Name", error.ConstraintName);
    }

    // lower() follows the database ctype: under a C-locale database it would keep "Ước" and "ước" apart.
    [Fact]
    public async Task M3_OnCLocaleDatabase_TreatsNonAsciiCaseDuplicatesAsEqual()
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
            await using NpgsqlConnection cLocale = new(builder.ConnectionString);
            await cLocale.OpenAsync();
            await using (NpgsqlCommand setup = new("""
                CREATE TABLE roles ("Id" uuid PRIMARY KEY, "OrganizationId" uuid NOT NULL, "Name" varchar(100) NOT NULL,
                    "SystemRoleKind" varchar(20), "CreatedAt" timestamptz NOT NULL);
                CREATE UNIQUE INDEX "IX_roles_OrganizationId_Name" ON roles ("OrganizationId", "Name");
                INSERT INTO roles VALUES
                    ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-0000000000a1', 'Ước', NULL, '2026-01-01'),
                    ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-0000000000a1', 'ước', NULL, '2026-01-02');
                """, cLocale))
            {
                await setup.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand migrate = new(RenameDuplicateRoleNames.UpSql, cLocale))
            {
                await migrate.ExecuteNonQueryAsync();
            }

            await using NpgsqlCommand read = new("""SELECT "Name" FROM roles ORDER BY "CreatedAt" """, cLocale);
            await using NpgsqlDataReader reader = await read.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Ước", reader.GetString(0));
            Assert.True(await reader.ReadAsync());
            Assert.Equal("ước (2)", reader.GetString(0));
        }
        finally
        {
            await using NpgsqlConnection admin = new(fixture.ConnectionString);
            await admin.OpenAsync();
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task M4_DeletesRolesOnlyOfInactiveMembers()
    {
        Guid org = await SeedOrganizationAsync();
        Guid role = await SeedRoleAsync(org, "Tester");
        Account active = Account.Create(new AccountParams("A", $"a-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@example.com"));
        Account inactive = Account.Create(new AccountParams("B", $"b-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@example.com"));
        context.Accounts.AddRange(active, inactive);
        OrganizationMember activeMember = OrganizationMember.Create(new OrganizationMemberParams(org, active.Id));
        OrganizationMember inactiveMember = OrganizationMember.Create(new OrganizationMemberParams(org, inactive.Id));
        inactiveMember.Deactivate();
        context.Set<OrganizationMember>().AddRange(activeMember, inactiveMember);
        context.Set<OrganizationMemberRole>().AddRange(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(activeMember.Id, role)),
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(inactiveMember.Id, role)));
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync(RemoveRolesOfInactiveMembers.UpSql);

        Guid[] seeded = [activeMember.Id, inactiveMember.Id];
        List<Guid> remaining = await context.Set<OrganizationMemberRole>()
            .AsNoTracking().Where(r => seeded.Contains(r.OrganizationMemberId))
            .Select(r => r.OrganizationMemberId).ToListAsync();
        Assert.Equal([activeMember.Id], remaining);
    }

    [Fact]
    public async Task M5_AddsFeedbackHubAdminToEveryAdminRole_RunTwiceAddsNothing()
    {
        Guid orgA = await SeedOrganizationAsync();
        Guid orgB = await SeedOrganizationAsync();
        Guid adminA = await SeedRoleAsync(orgA, "Admin", SystemRoleKind.Admin);
        Guid adminB = await SeedRoleAsync(orgB, "Admin", SystemRoleKind.Admin);
        Guid member = await SeedRoleAsync(orgA, "Member", SystemRoleKind.Member);
        context.Set<RolePermission>().Add(RolePermission.Create(new RolePermissionParams(adminB, "feedbackhub.admin")));
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync(GrantFeedbackHubAdminToAdminRoles.UpSql);
        await context.Database.ExecuteSqlRawAsync(GrantFeedbackHubAdminToAdminRoles.UpSql);

        Guid[] seeded = [adminA, adminB, member];
        List<Guid> holders = await context.Set<RolePermission>().AsNoTracking()
            .Where(p => p.PermissionCode == "feedbackhub.admin" && seeded.Contains(p.RoleId))
            .Select(p => p.RoleId).ToListAsync();
        Assert.Equal(1, holders.Count(id => id == adminA));
        Assert.Equal(1, holders.Count(id => id == adminB));
        Assert.DoesNotContain(member, holders);
    }

    // AC-290, AC-326: both requests pass the service's name check before either saves; the index decides.
    // Commits rows on purpose, the unique index only fires across two real transactions.
    [Fact]
    public async Task SaveChanges_ConcurrentSameNameDifferentCase_SecondThrowsConflict()
    {
        Guid org;
        await using (AppDbContext seed = fixture.CreateDbContext())
        {
            Organization organization = Organization.Create(new OrganizationParams("Org", $"race-{Guid.NewGuid():N}"));
            seed.Organizations.Add(organization);
            await seed.SaveChangesAsync();
            org = organization.Id;
        }

        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        firstContext.Set<Role>().Add(Role.Create(new RoleParams(org, "Tester")));
        secondContext.Set<Role>().Add(Role.Create(new RoleParams(org, "TESTER")));
        await using ServiceProvider firstServices = new ServiceCollection().BuildServiceProvider();
        await using ServiceProvider secondServices = new ServiceCollection().BuildServiceProvider();
        UnitOfWork first = new(firstContext, firstServices);
        UnitOfWork second = new(secondContext, secondServices);

        await first.SaveChangesAsync();
        ConflictException conflict = await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());

        Assert.Equal(ApplicationMessages.OrganizationRoleNameAlreadyExists, conflict.Message);
    }
}
