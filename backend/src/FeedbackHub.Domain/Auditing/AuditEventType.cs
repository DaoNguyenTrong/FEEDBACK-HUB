namespace FeedbackHub.Domain.Auditing;

/// <summary>The closed catalogue of audited events (TECH00 §3.7). Values are never reused.</summary>
public enum AuditEventType
{
    InvitationSent = 1,
    InvitationRevoked = 2,
    InvitationDeleted = 3,
    MemberJoined = 4,
    MemberRolesChanged = 5,
    MemberRemoved = 6,
    MemberLeft = 7,
    RoleCreated = 8,
    RoleUpdated = 9,
    RoleDeleted = 10,
    OrganizationRenamed = 11,
    OrganizationDeactivated = 12,
    OrganizationReactivated = 13
}
