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
        context = fixture.CreateDbContext(sqlCapture);
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
        Assert.Equal(DateTimeKind.Utc, row.Timestamp.Kind);
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
        // EXPLAIN of the exact command EF ran: the keyset must be an Index Cond (a seek), not a Filter that reads
        // every newer row first. The literal kind is what lets the planner match the partial index's WHERE clause.
        Guid organizationId = Guid.NewGuid();
        Account actor = await SeedAccountAsync("Plan Actor");
        await SeedAsync(EventRow(organizationId, BaseTime, userId: actor.Id.ToString()));
        sqlCapture.Commands.Clear();
        // The cursor is later than the row, so the page is non-empty whatever the id sequence has reached.
        await ListAsync(organizationId, new AuditCursor(BaseTime.AddMinutes(1), 0));
        Assert.Equal(2, sqlCapture.Commands.Count);
        CapturedCommand query = sqlCapture.Commands[0];
        CapturedCommand namesQuery = sqlCapture.Commands[1];

        (string? indexCond, bool hasSort) = await KeysetPlanAsync(query);

        // A Hash Join plus Sort would read every older row; the index must deliver the order and stop at the limit.
        Assert.False(hasSort, "The keyset plan must not contain a Sort node.");
        Assert.Contains("\"timestamp\"", indexCond);
        Assert.Contains("(a.timestamp, a.id) < (@", query.Text);
        Assert.Contains("a.kind = 'Event'", query.Text);
        Assert.Contains("a.organization_id = @", query.Text);
        Assert.DoesNotContain("JOIN", query.Text);
        Assert.Contains("FROM accounts", namesQuery.Text);
        // Npgsql renders the Guid list as "= ANY (@array)", the IN form on the primary key column.
        Assert.Contains("a.\"Id\" = ANY (@", namesQuery.Text);
    }

    [Fact]
    public async Task ListEventsAsync_NonGuidUserId_ActorNameNullWithoutFailure()
    {
        Guid organizationId = Guid.NewGuid();
        await SeedAsync(EventRow(organizationId, BaseTime, userId: "not-a-guid"));
        sqlCapture.Commands.Clear();

        AuditEventRow row = Assert.Single(await ListAsync(organizationId));

        Assert.Null(row.ActorName);
        Assert.Single(sqlCapture.Commands);
    }

    private async Task<(string? IndexCond, bool HasSort)> KeysetPlanAsync(CapturedCommand query)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await using DbCommand explain = connection.CreateCommand();
        explain.Transaction = transaction.GetDbTransaction();
        explain.CommandText = "EXPLAIN (FORMAT JSON) " + query.Text;
        foreach ((string name, object? value) in query.Parameters)
        {
            DbParameter parameter = explain.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            explain.Parameters.Add(parameter);
        }

        using JsonDocument plan = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);
        JsonElement root = plan.RootElement[0].GetProperty("Plan");
        return (FindKeysetScan(root)?.GetProperty("Index Cond").GetString(), ContainsSort(root));
    }

    private static bool ContainsSort(JsonElement node) =>
        (node.TryGetProperty("Node Type", out JsonElement type) && type.GetString() is "Sort" or "Incremental Sort")
        || (node.TryGetProperty("Plans", out JsonElement children) && children.EnumerateArray().Any(ContainsSort));

    private static JsonElement? FindKeysetScan(JsonElement node)
    {
        if (node.TryGetProperty("Index Name", out JsonElement index) && index.GetString() == "IX_audit_logs_event_keyset")
        {
            return node;
        }

        if (node.TryGetProperty("Plans", out JsonElement children))
        {
            foreach (JsonElement child in children.EnumerateArray())
            {
                if (FindKeysetScan(child) is JsonElement found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private sealed record CapturedCommand(string Text, IReadOnlyList<(string Name, object? Value)> Parameters);

    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<CapturedCommand> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(new CapturedCommand(
                command.CommandText,
                command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, (object?)p.Value)).ToList()));
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
