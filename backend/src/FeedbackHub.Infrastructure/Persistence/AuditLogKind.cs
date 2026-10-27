namespace FeedbackHub.Infrastructure.Persistence;

/// <summary>
/// Stored by name. No 0 member, so a row whose kind was never assigned cannot silently read as an event.
/// </summary>
public enum AuditLogKind
{
    /// <summary>Automatic entity snapshot written by <c>AppDbContext</c>.</summary>
    Change = 1,

    /// <summary>Semantic event recorded through <c>IAuditTrail</c>.</summary>
    Event = 2
}
