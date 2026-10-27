using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Domain.Auditing;

/// <summary>A role as it was when an event happened: a snapshot, so a later rename or delete cannot rewrite history.</summary>
public sealed record AuditRoleRef(Guid Id, string Name, SystemRoleKind? SystemKind)
{
    public static AuditRoleRef From(Role role) => new(role.Id, role.Name, role.SystemRoleKind);
}
