using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Migrations;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence.Migrations;

[Collection(nameof(PostgresCollectionB))]
public sealed class ReverifyMicrosoftOnlyAccountsTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    // docs/tech/00-nen-tang.md §10.2 step 6, query (1).
    private const string JustMarkedQuery = """
        SELECT a."Id", a."Username", a."Email"
        FROM accounts a
        WHERE a."EmailConfirmed" = false AND a."PasswordHash" IS NULL
          AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
          AND NOT EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
        ORDER BY a."Username"
        """;

    // docs/tech/00-nen-tang.md §10.2 step 6, query (2).
    private const string ReviewQuery = """
        SELECT a."Id", a."Username", a."Email", 'PasswordAndMicrosoftLink' AS reason, NULL::text AS previous_email, NULL::timestamptz AS changed_at
        FROM accounts a
        WHERE a."PasswordHash" IS NOT NULL
          AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
        UNION ALL
        SELECT a."Id", a."Username", a."Email", 'MicrosoftAndGoogleLinks', NULL, NULL
        FROM accounts a
        WHERE EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Microsoft')
          AND EXISTS (SELECT 1 FROM external_logins l WHERE l.account_id = a."Id" AND l.provider = 'Google')
        UNION ALL
        SELECT a."Id", a."Username", a."Email", 'EmailChangedInAudit', t.previous_email, t."timestamp"
        FROM (
          SELECT entity_id, "timestamp", new_values ->> 'Email' AS email,
                 LAG(new_values ->> 'Email') OVER (PARTITION BY entity_id ORDER BY "timestamp", id) AS previous_email
          FROM audit_logs
          WHERE entity_name = 'Account' AND new_values ? 'Email'
        ) t
        JOIN accounts a ON a."Id"::text = t.entity_id
        WHERE t.previous_email IS NOT NULL AND t.previous_email <> t.email
        ORDER BY 2, 6
        """;

    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<Account> SeedAccountAsync(string tag, bool confirmed, bool password, params string[] providers)
    {
        Account account = Account.Create(new AccountParams($"Name {tag}", $"user-{tag}", $"{tag}@example.com"));
        if (confirmed)
        {
            account.ConfirmEmail();
        }

        if (password)
        {
            account.SetPasswordHash("hash-value");
        }

        context.Accounts.Add(account);
        foreach (string provider in providers)
        {
            context.Set<ExternalLogin>().Add(ExternalLogin.Create(
                new ExternalLoginParams(account.Id, provider, $"{provider}-{tag}", $"{tag}@example.com")));
        }

        await context.SaveChangesAsync();
        return account;
    }

    private async Task<Dictionary<Guid, bool>> ConfirmedByIdAsync(params Account[] accounts)
    {
        Guid[] ids = accounts.Select(a => a.Id).ToArray();
        return await context.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.EmailConfirmed);
    }

    private async Task<Account[]> SeedFiveAsync()
    {
        Account msOnly = await SeedAccountAsync("msonly", true, false, "Microsoft");
        Account msPassword = await SeedAccountAsync("mspass", true, true, "Microsoft");
        Account msGoogle = await SeedAccountAsync("msgoogle", true, false, "Microsoft", "Google");
        Account googleOnly = await SeedAccountAsync("googleonly", true, false, "Google");
        Account msUnverified = await SeedAccountAsync("msfalse", false, false, "Microsoft");
        return [msOnly, msPassword, msGoogle, googleOnly, msUnverified];
    }

    // UpSql
    [Fact]
    public async Task UpSql_MarksOnlyPasswordlessMicrosoftWithoutGoogle()
    {
        Account[] seeded = await SeedFiveAsync();

        await context.Database.ExecuteSqlRawAsync(ReverifyMicrosoftOnlyAccounts.UpSql);

        Dictionary<Guid, bool> confirmed = await ConfirmedByIdAsync(seeded);
        Assert.False(confirmed[seeded[0].Id]);
        Assert.True(confirmed[seeded[1].Id]);
        Assert.True(confirmed[seeded[2].Id]);
        Assert.True(confirmed[seeded[3].Id]);
        Assert.False(confirmed[seeded[4].Id]);
    }

    [Fact]
    public async Task UpSql_RunTwice_ChangesNothingMore()
    {
        Account[] seeded = await SeedFiveAsync();
        await context.Database.ExecuteSqlRawAsync(ReverifyMicrosoftOnlyAccounts.UpSql);
        Dictionary<Guid, bool> afterFirst = await ConfirmedByIdAsync(seeded);

        List<string> notices = await RunUpSqlCapturingNoticesAsync();

        Assert.Equal(afterFirst, await ConfirmedByIdAsync(seeded));
        Assert.Contains(notices, n => n.StartsWith("M8 marked 0 account(s)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpSql_Notice_ContainsNoEmailOrUsername()
    {
        Account[] seeded = await SeedFiveAsync();
        List<string> notices = await RunUpSqlCapturingNoticesAsync();

        string notice = Assert.Single(notices, n => n.StartsWith("M8", StringComparison.Ordinal));
        Assert.Contains("1 account(s)", notice);
        Assert.Contains(seeded[0].Id.ToString(), notice);
        foreach (Account account in seeded)
        {
            Assert.DoesNotContain(account.Email, notice);
            Assert.DoesNotContain(account.Username, notice);
        }

        foreach (Account other in seeded.Skip(1))
        {
            Assert.DoesNotContain(other.Id.ToString(), notice);
        }
    }

    // ReviewQueries
    [Fact]
    public async Task ReviewQueries_Section10_2Step6_RunOnNewSchemaAndReturnSeededRows()
    {
        Account[] seeded = await SeedFiveAsync();
        await context.Database.ExecuteSqlRawAsync(ReverifyMicrosoftOnlyAccounts.UpSql);

        List<(Guid Id, string Reason)> marked = await ReadAsync(JustMarkedQuery, reasonColumn: -1);
        List<(Guid Id, string Reason)> review = await ReadAsync(ReviewQuery, reasonColumn: 3);

        Assert.Equal(
            new[] { seeded[0].Id, seeded[4].Id }.OrderBy(i => i),
            marked.Select(m => m.Id).OrderBy(i => i));
        Assert.Contains((seeded[1].Id, "PasswordAndMicrosoftLink"), review);
        Assert.Contains((seeded[2].Id, "MicrosoftAndGoogleLinks"), review);
    }

    private async Task<List<string>> RunUpSqlCapturingNoticesAsync()
    {
        List<string> notices = [];
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        NoticeEventHandler handler = (_, e) => notices.Add(e.Notice.MessageText);
        connection.Notice += handler;
        try
        {
            await context.Database.ExecuteSqlRawAsync(ReverifyMicrosoftOnlyAccounts.UpSql);
        }
        finally
        {
            connection.Notice -= handler;
        }

        return notices;
    }

    private async Task<List<(Guid Id, string Reason)>> ReadAsync(string sql, int reasonColumn)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using NpgsqlCommand command = new(sql, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<(Guid, string)> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reasonColumn < 0 ? string.Empty : reader.GetString(reasonColumn)));
        }

        return rows;
    }
}
