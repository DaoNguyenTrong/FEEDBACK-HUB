using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Services.AuditLogs;

namespace FeedbackHub.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditEventRow>> ListEventsAsync(
        Guid organizationId,
        AuditCursor? before,
        int take,
        CancellationToken cancellationToken)
    {
        // Tenant and kind filters stay in SQL; the enum constant compiles to the literal 'Event' that the
        // partial index IX_audit_logs_event_keyset requires to qualify.
        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking()
            .Where(log => log.OrganizationId == organizationId && log.Kind == AuditLogKind.Event);

        if (before is AuditCursor cursor)
        {
            DateTime timestamp = cursor.TimestampUtc;
            long id = cursor.Id;
            // A row comparison, not "t < @t OR (t = @t AND id < @id)": Postgres seeks the index only on the former;
            // the OR form scans every newer row of the organization as a Filter.
            query = query.Where(log =>
                EF.Functions.LessThan(ValueTuple.Create(log.Timestamp, log.Id), ValueTuple.Create(timestamp, id)));
        }

        return await (
            from log in query
            join account in dbContext.Accounts.AsNoTracking()
                on log.UserId equals account.Id.ToString() into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            orderby log.Timestamp descending, log.Id descending
            select new AuditEventRow(
                log.Id,
                log.Timestamp,
                log.EntityName,
                log.Action,
                log.ActorKind,
                account != null ? account.Name : null,
                log.NewValues))
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
