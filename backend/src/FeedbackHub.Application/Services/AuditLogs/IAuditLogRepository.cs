namespace FeedbackHub.Application.Services.AuditLogs;

public interface IAuditLogRepository
{
    /// <summary>
    /// The organization's <c>Event</c> rows strictly older than <paramref name="before"/> (all when <c>null</c>),
    /// newest first by timestamp then id, at most <paramref name="take"/> of them.
    /// </summary>
    Task<IReadOnlyList<AuditEventRow>> ListEventsAsync(
        Guid organizationId,
        AuditCursor? before,
        int take,
        CancellationToken cancellationToken);
}
