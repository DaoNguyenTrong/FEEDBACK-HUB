using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Repositories;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence.Repositories;

[Collection(nameof(PostgresCollectionB))]
public sealed class AuditLogRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime BaseTime = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly SqlCapture sqlCapture = new();
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private AuditLogRepository repository = null!;

    public async Task InitializeAsync()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(sqlCapture)
            .Options;
        context = new AppDbContext(options);
        transaction = await context.Database.BeginTransactionAsync();
        repository = new AuditLogRepository(context);
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private static AuditLog EventRow(
        Guid? organizationId,
        DateTime timestamp,
        string? actorKind = "Account",
        string? userId = null,
        string? newValues = null) =>
        new()
        {
            EntityName = "OrganizationMember",
            EntityId = Guid.NewGuid().ToString(),
            Action = "MemberLeft",
            NewValues = newValues,
            UserId = userId,
            OrganizationId = organizationId,
            Timestamp = timestamp,
            Kind = AuditLogKind.Event,
            ActorKind = actorKind
        };

    private async Task SeedAsync(params AuditLog[] logs)
    {
        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync();
    }

    private async Task<Account> SeedAccountAsync(string name)
    {
        string handle = $"audit-{Guid.NewGuid():N}";
        Account account = Account.Create(new AccountParams(name, handle, $"{handle}@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    private Task<IReadOnlyList<AuditEventRow>> ListAsync(Guid organizationId, AuditCursor? before = null, int take = 50) =>
        repository.ListEventsAsync(organizationId, before, take, CancellationToken.None);

    private static AuditCursor CursorOf(AuditEventRow row) => new(row.Timestamp, row.Id);

    // ListEventsAsync

    [Fact]
    public async Task ListEventsAsync_ReturnsOnlyEventRowsOfThatOrganization()
    {
        // The organization's own Change rows (its ApiKey snapshot, B2) must stay out of the feed.
        Organization organization = Organization.Create(
            new OrganizationParams("Audit feed org", $"audit-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        context.ApiKeys.Add(ApiKey.Create(new ApiKeyParams("Snapshot key"), "pfx", "hash", organization.Id));
        await context.SaveChangesAsync();
        AuditLog own = EventRow(organization.Id, BaseTime);
        await SeedAsync(own, EventRow(Guid.NewGuid(), BaseTime), EventRow(null, BaseTime));
        Assert.True(await context.AuditLogs.AnyAsync(log =>
            log.OrganizationId == organization.Id && log.Kind == AuditLogKind.Change));

        IReadOnlyList<AuditEventRow> rows = await ListAsync(organization.Id);

        Assert.Equal([own.Id], rows.Select(row => row.Id));
    }

    [Fact]
    public async Task ListEventsAsync_MapsTheRowColumns()
    {
        Guid organizationId = Guid.NewGuid();
        AuditLog log = EventRow(organizationId, BaseTime, newValues: """{"email": "x@y.z"}""");
        await SeedAsync(log);

        AuditEventRow row = Assert.Single(await ListAsync(organizationId));

        Assert.Equal(
            (log.Id, BaseTime, "OrganizationMember", "MemberLeft", "Account"),
            (row.Id, row.Timestamp, row.EntityName, row.Action, row.ActorKind));
        using JsonDocument values = JsonDocument.Parse(row.NewValues!);
        Assert.Equal("x@y.z", values.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ListEventsAsync_OrdersByTimestampDescThenIdDesc()
    {
        Guid organizationId = Guid.NewGuid();
        AuditLog oldest = EventRow(organizationId, BaseTime.AddMinutes(-5));
        AuditLog tieFirst = EventRow(organizationId, BaseTime);
        AuditLog tieSecond = EventRow(organizationId, BaseTime);
        AuditLog newest = EventRow(organizationId, BaseTime.AddMinutes(5));
        await SeedAsync(oldest, tieFirst, newest);
        await SeedAsync(tieSecond);

        IReadOnlyList<AuditEventRow> rows = await ListAsync(organizationId);

        Assert.Equal([newest.Id, tieSecond.Id, tieFirst.Id, oldest.Id], rows.Select(row => row.Id));
    }

    [Fact]
    public async Task ListEventsAsync_ManyRowsSameTimestamp_PagesWithoutRepeatOrGap()
    {
        // One SaveChanges stamps a single `now` on every row it writes, so ties on timestamp are the norm.
        Guid organizationId = Guid.NewGuid();
        AuditLog[] logs = Enumerable.Range(0, 5).Select(_ => EventRow(organizationId, BaseTime)).ToArray();
        await SeedAsync(logs);

        List<long> paged = [];
        AuditCursor? cursor = null;
        for (int page = 0; page < 3; page++)
        {
            IReadOnlyList<AuditEventRow> rows = await ListAsync(organizationId, cursor, take: 2);
            paged.AddRange(rows.Select(row => row.Id));
            cursor = rows.Count > 0 ? CursorOf(rows[^1]) : cursor;
        }

        Assert.Equal(logs.Select(log => log.Id).OrderByDescending(id => id), paged);
    }

    [Fact]
    public async Task ListEventsAsync_RowInsertedBeforeFirstPageAfterRead_NextPageUnaffected()
    {
        // AC-360: "Xem thêm" neither repeats nor skips an entry when a newer one arrives between two pages.
        Guid organizationId = Guid.NewGuid();
        AuditLog[] logs = Enumerable.Range(0, 4).Select(i => EventRow(organizationId, BaseTime.AddMinutes(-i))).ToArray();
        await SeedAsync(logs);
        IReadOnlyList<AuditEventRow> firstPage = await ListAsync(organizationId, take: 2);
        await SeedAsync(EventRow(organizationId, BaseTime.AddMinutes(10)));

        IReadOnlyList<AuditEventRow> secondPage = await ListAsync(organizationId, CursorOf(firstPage[^1]), take: 2);

        Assert.Equal([logs[2].Id, logs[3].Id], secondPage.Select(row => row.Id));
    }

    [Fact]
    public async Task ListEventsAsync_ActorNameIsCurrentAccountName()
    {
        Guid organizationId = Guid.NewGuid();
        Account actor = await SeedAccountAsync("Old Name");
        await SeedAsync(EventRow(organizationId, BaseTime, userId: actor.Id.ToString()));
        actor.Update(new AccountParams("New Name", actor.Username, actor.Email));
        await context.SaveChangesAsync();

        AuditEventRow row = Assert.Single(await ListAsync(organizationId));

        Assert.Equal("New Name", row.ActorName);
    }

    [Fact]
    public async Task ListEventsAsync_ActorAccountDeleted_ActorNameNull()
    {
        Guid organizationId = Guid.NewGuid();
        Account actor = await SeedAccountAsync("Gone Soon");
        await SeedAsync(EventRow(organizationId, BaseTime, userId: actor.Id.ToString()));
        context.Accounts.Remove(actor);
        await context.SaveChangesAsync();

        AuditEventRow row = Assert.Single(await ListAsync(organizationId));

        Assert.Null(row.ActorName);
    }

    [Fact]
    public async Task ListEventsAsync_ReturnsRawActorKindIncludingNullAndUnknown()
    {
        // The repository never interprets actor_kind; the service decides what to do with odd values (C2).
        Guid organizationId = Guid.NewGuid();
        await SeedAsync(
            EventRow(organizationId, BaseTime.AddMinutes(2), actorKind: "Account"),
            EventRow(organizationId, BaseTime.AddMinutes(1), actorKind: null),
            EventRow(organizationId, BaseTime, actorKind: "Bogus"));

        IReadOnlyList<AuditEventRow> rows = await ListAsync(organizationId);

        Assert.Equal(["Account", null, "Bogus"], rows.Select(row => row.ActorKind));
    }

    [Fact]
    public async Task ListEventsAsync_UsesPartialEventIndex()
    {
        // First check: the partial index can serve the keyset predicate and order. Second: EF emits that
        // predicate with a literal kind, which the planner needs to match the index's WHERE clause.
        Guid organizationId = Guid.NewGuid();
        await SeedAsync(EventRow(organizationId, BaseTime));
        await context.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");

        string plan = await ExplainAsync($"""
            EXPLAIN (FORMAT JSON)
            SELECT a.id FROM audit_logs AS a
            WHERE a.organization_id = '{organizationId}' AND a.kind = 'Event'
              AND (a."timestamp" < '2026-10-01T08:00:00Z' OR (a."timestamp" = '2026-10-01T08:00:00Z' AND a.id < 100))
            ORDER BY a."timestamp" DESC, a.id DESC
            LIMIT 51
            """);
        sqlCapture.Commands.Clear();
        await ListAsync(organizationId, new AuditCursor(BaseTime, 100));

        Assert.Contains("IX_audit_logs_event_keyset", plan);
        Assert.Contains(sqlCapture.Commands, sql => sql.Contains(".kind = 'Event'") && sql.Contains(".organization_id = @"));
    }

    private async Task<string> ExplainAsync(string sql)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
