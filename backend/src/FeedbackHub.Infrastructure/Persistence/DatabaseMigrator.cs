using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Persistence;

internal sealed class DatabaseMigrator(AppDbContext dbContext) : IDatabaseMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
