using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Domain.Auditing;

/// <summary>
/// One audited event. The organization is always copied from the entity the event describes, never passed in
/// as a bare id, so a caller cannot attribute an event to the wrong tenant.
/// </summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public Guid OrganizationId { get; private init; }

    public Guid SubjectId { get; private init; }

    public AuditEventType Type { get; private init; }

    public ActorKind ActorKind { get; private init; }

    public Guid? ActorAccountId { get; private init; }

    public IReadOnlyDictionary<string, object> Parameters { get; private init; } =
        new Dictionary<string, object>().AsReadOnly();

    public static AuditEvent For<TSubject>(
        TSubject subject,
        AuditEventType type,
        ActorKind actorKind,
        Guid? actorAccountId,
        IReadOnlyDictionary<string, object>? parameters = null)
        where TSubject : BaseEntity<Guid>, IHasOrganization
    {
        ArgumentNullException.ThrowIfNull(subject);
        return Build(subject.OrganizationId, subject.Id, type, actorKind, actorAccountId, parameters);
    }

    public static AuditEvent For(
        Organization organization,
        AuditEventType type,
        ActorKind actorKind,
        Guid? actorAccountId,
        IReadOnlyDictionary<string, object>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(organization);
        return Build(organization.Id, organization.Id, type, actorKind, actorAccountId, parameters);
    }

    private static AuditEvent Build(
        Guid organizationId,
        Guid subjectId,
        AuditEventType type,
        ActorKind actorKind,
        Guid? actorAccountId,
        IReadOnlyDictionary<string, object>? parameters)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An audit event needs the organization of its subject.", nameof(organizationId));
        }

        return new AuditEvent
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            Type = type,
            ActorKind = actorKind,
            ActorAccountId = actorAccountId,
            Parameters = (parameters is null ? [] : new Dictionary<string, object>(parameters)).AsReadOnly()
        };
    }
}
