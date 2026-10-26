using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Auditing;

namespace FeedbackHub.Application.Services.AuditLogs;

/// <summary>One semantic audit entry, as the organization's log shows it.</summary>
/// <param name="ActorName">The actor's current name; only for <c>Account</c> actors, <c>null</c> once deleted.</param>
/// <param name="Parameters">Only the keys the event type declares: text, text lists, roles or role lists.</param>
public sealed record AuditEntryDto(
    long Id,
    DateTime OccurredAt,
    ActorKind ActorKind,
    string? ActorName,
    AuditEventType Type,
    IReadOnlyDictionary<string, object> Parameters);

/// <param name="NextCursor">
/// Pass it as <c>cursor</c> to read the next page. A page can hold fewer than <c>limit</c> items, even none, while
/// this is not <c>null</c>; stop paging only when it is <c>null</c>.
/// </param>
public sealed record AuditEntryPage(IReadOnlyList<AuditEntryDto> Items, string? NextCursor);
