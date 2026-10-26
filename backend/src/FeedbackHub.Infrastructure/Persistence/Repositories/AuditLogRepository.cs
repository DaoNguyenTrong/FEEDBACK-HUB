using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Services.AuditLogs;

namespace FeedbackHub.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> ListPagedAsync(
        Guid organizationId,
        int pageNumber,
        int pageSize,
        string? search,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking()
            .Where(log => log.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search}%";
            query = query.Where(log =>
                EF.Functions.ILike(log.EntityName, pattern) ||
                EF.Functions.ILike(log.EntityId, pattern) ||
                EF.Functions.ILike(log.Action, pattern) ||
                (log.UserId != null && EF.Functions.ILike(log.UserId, pattern)));
        }

        if (userId is not null)
        {
            string userIdText = userId.Value.ToString();
            query = query.Where(log => log.UserId == userIdText);
        }

        if (systemOnly == true)
        {
            query = query.Where(log => log.UserId == null);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        IReadOnlyList<AuditLogDto> items = await (
            from log in query
            join account in dbContext.Accounts.AsNoTracking()
                on log.UserId equals account.Id.ToString() into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            // Id tie-breaks the many rows one SaveChanges batch stamps with the same Timestamp;
            // without it, Postgres may order ties differently per page and rows repeat/vanish.
            orderby log.Timestamp descending, log.Id descending
            select new AuditLogDto(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.OldValues,
                log.NewValues,
                log.UserId,
                account != null ? account.Name : null,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<AuditLogDto?> GetByIdAsync(Guid organizationId, long id, CancellationToken cancellationToken = default)
    {
        return (
            from log in dbContext.AuditLogs.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on log.UserId equals account.Id.ToString() into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            where log.Id == id && log.OrganizationId == organizationId
            select new AuditLogDto(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.OldValues,
                log.NewValues,
                log.UserId,
                account != null ? account.Name : null,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp))
            .FirstOrDefaultAsync(cancellationToken);
    }

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
