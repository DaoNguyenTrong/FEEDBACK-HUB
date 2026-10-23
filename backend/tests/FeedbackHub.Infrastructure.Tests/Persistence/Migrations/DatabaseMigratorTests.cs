using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence.Migrations;

[Collection(nameof(PostgresCollection))]
public sealed class DatabaseMigratorTests(PostgresContainerFixture fixture)
{
    // MigrateAsync
    [Fact]
    public async Task MigrateAsync_FreshDatabase_ForwardsMigrationNoticesToTheLog()
    {
        string database = $"migrator_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = new($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            NpgsqlConnectionStringBuilder builder = new(fixture.ConnectionString) { Database = database, Pooling = false };
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options;
            await using AppDbContext context = new(options);
            CapturingLogger logger = new();

            await new DatabaseMigrator(context, logger).MigrateAsync(CancellationToken.None);

            Assert.Contains(logger.Messages, m => m.StartsWith("Migration notice: M8 marked 0 account(s)", StringComparison.Ordinal));
        }
        finally
        {
            await using NpgsqlConnection admin = new(fixture.ConnectionString);
            await admin.OpenAsync();
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class CapturingLogger : ILogger<DatabaseMigrator>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
