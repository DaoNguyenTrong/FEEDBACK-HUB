using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Services.OrganizationLocking;

namespace FeedbackHub.Infrastructure.Persistence;

internal sealed class OrganizationLock(AppDbContext dbContext) : IOrganizationLock
{
    public async Task AcquireAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Locking an organization row requires an open transaction.");
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM organizations WHERE "Id" = {organizationId} FOR UPDATE""", cancellationToken);
    }
}
