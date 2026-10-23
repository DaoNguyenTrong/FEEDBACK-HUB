using FeedbackHub.Domain.Auditing;

namespace FeedbackHub.Application.Services.AuditTrail;

public interface IAuditTrail
{
    /// <summary>
    /// Only stages the row in the unit of work; it is written by the caller's next <c>SaveChangesAsync</c>, in
    /// the same transaction as the change it describes.
    /// </summary>
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
