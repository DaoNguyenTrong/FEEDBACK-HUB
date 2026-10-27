namespace FeedbackHub.Application.Services.AuditLogs;

/// <summary>
/// One stored semantic audit entry. <see cref="ActorKind"/> and <see cref="NewValues"/> are the raw column text,
/// unvalidated; <see cref="ActorName"/> is the actor account's current name, or <c>null</c> when none matches.
/// </summary>
public sealed record AuditEventRow(
    long Id,
    DateTime Timestamp,
    string EntityName,
    string Action,
    string? ActorKind,
    string? ActorName,
    string? NewValues);
