namespace FeedbackHub.Application.Services.AuditLogs;

public interface IAuditLogService
{
    Task<AuditEntryPage> ListAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
