using System.Text.Json;
using System.Text.Json.Serialization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Domain.Auditing;

namespace FeedbackHub.Infrastructure.Persistence;

/// <summary>
/// Stages one <see cref="AuditLogKind.Event"/> row in the unit of work. Deliberately takes no tenant, user or HTTP
/// context: the organization and actor come from the event itself, and the row is committed (or rolled back)
/// by the caller's own <c>SaveChangesAsync</c>.
/// </summary>
internal sealed class AuditTrail(AppDbContext dbContext, IDateTimeProvider dateTimeProvider) : IAuditTrail
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        AuditEventCatalog.Validate(auditEvent);

        dbContext.AuditLogs.Add(new AuditLog
        {
            Kind = AuditLogKind.Event,
            Action = auditEvent.Type.ToString(),
            EntityName = AuditEventCatalog.Get(auditEvent.Type).EntityName,
            EntityId = auditEvent.SubjectId.ToString(),
            OrganizationId = auditEvent.OrganizationId,
            UserId = auditEvent.ActorAccountId?.ToString(),
            ActorKind = auditEvent.ActorKind.ToString(),
            NewValues = auditEvent.Parameters.Count > 0
                ? JsonSerializer.Serialize(auditEvent.Parameters, JsonOptions)
                : null,
            OldValues = null,
            IpAddress = null,
            UserAgent = null,
            Timestamp = dateTimeProvider.UtcNow
        });

        return Task.CompletedTask;
    }
}
