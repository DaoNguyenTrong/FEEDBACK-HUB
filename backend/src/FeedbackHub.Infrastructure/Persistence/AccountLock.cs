using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.Infrastructure.Persistence;

internal sealed class AccountLock(AppDbContext dbContext) : IAccountLock
{
    public async Task AcquireAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Locking an account row requires an open transaction.");
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM accounts WHERE "Id" = {accountId} FOR UPDATE""", cancellationToken);
    }
}
