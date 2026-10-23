using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Persistence;

internal sealed class DatabaseMigrator(AppDbContext dbContext, ILogger<DatabaseMigrator> logger) : IDatabaseMigrator
{
    // Data migrations report what they changed through RAISE NOTICE (e.g. M8); the operator reads it in the log.
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection? connection = dbContext.Database.GetDbConnection() as NpgsqlConnection;
        NoticeEventHandler handler = (_, e) => logger.LogInformation("Migration notice: {Notice}", e.Notice.MessageText);
        if (connection is not null)
        {
            connection.Notice += handler;
        }

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            if (connection is not null)
            {
                connection.Notice -= handler;
            }
        }
    }
}
