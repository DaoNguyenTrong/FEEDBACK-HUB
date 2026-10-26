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

        // Two reads, not a LEFT JOIN on "UserId = Id::text": the cast cannot use an accounts index, so the planner
        // chose Hash Join + Sort and read every older event row of the organization, defeating the keyset.
        var page = await query
            .OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.Id)
            .Select(log => new { log.Id, log.Timestamp, log.EntityName, log.Action, log.ActorKind, log.UserId, log.NewValues })
            .Take(take)
            .ToListAsync(cancellationToken);

        // user_id is free text: only values that parse as a Guid can name an account; the rest get no name.
        List<Guid> userIds = page
            .Select(log => Guid.TryParse(log.UserId, out Guid userId) ? userId : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        Dictionary<Guid, string> names = userIds.Count == 0
            ? []
            : await dbContext.Accounts.AsNoTracking()
                .Where(account => userIds.Contains(account.Id))
                .Select(account => new { account.Id, account.Name })
                .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return page
            .Select(log => new AuditEventRow(
                log.Id,
                log.Timestamp,
                log.EntityName,
                log.Action,
                log.ActorKind,
                Guid.TryParse(log.UserId, out Guid userId) && names.TryGetValue(userId, out string? name) ? name : null,
                log.NewValues))
            .ToList();
    }
}
