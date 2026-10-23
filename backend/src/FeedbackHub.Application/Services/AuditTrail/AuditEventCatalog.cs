using FeedbackHub.Domain.Auditing;

namespace FeedbackHub.Application.Services.AuditTrail;

/// <summary>Value type of one audit parameter: <c>string</c>, <c>IReadOnlyList&lt;string&gt;</c>, <c>AuditRoleRef</c>, <c>IReadOnlyList&lt;AuditRoleRef&gt;</c>.</summary>
public enum AuditParameterKind
{
    Text,
    TextList,
    Role,
    RoleList
}

public sealed record AuditEventDefinition(string EntityName, IReadOnlyDictionary<string, AuditParameterKind> Parameters);

/// <summary>The closed catalogue of TECH00 §3.7: entity name and parameters of every <see cref="AuditEventType"/>.</summary>
public static class AuditEventCatalog
{
    private const string InvitationEntity = "OrganizationInvitation";
    private const string MemberEntity = "OrganizationMember";
    private const string RoleEntity = "Role";
    private const string OrganizationEntity = "Organization";

    private static readonly IReadOnlyDictionary<AuditEventType, AuditEventDefinition> Definitions =
        new Dictionary<AuditEventType, AuditEventDefinition>
        {
            [AuditEventType.InvitationSent] = Invitation(),
            [AuditEventType.InvitationRevoked] = Invitation(),
            [AuditEventType.InvitationDeleted] = Invitation(),
            [AuditEventType.MemberJoined] = Define(MemberEntity,
                ("accountName", AuditParameterKind.Text), ("email", AuditParameterKind.Text),
                ("role", AuditParameterKind.Role)),
            [AuditEventType.MemberRolesChanged] = Define(MemberEntity,
                ("accountName", AuditParameterKind.Text), ("email", AuditParameterKind.Text),
                ("rolesBefore", AuditParameterKind.RoleList), ("rolesAfter", AuditParameterKind.RoleList)),
            [AuditEventType.MemberRemoved] = Define(MemberEntity,
                ("accountName", AuditParameterKind.Text), ("email", AuditParameterKind.Text),
                ("roles", AuditParameterKind.RoleList)),
            [AuditEventType.MemberLeft] = Define(MemberEntity,
                ("accountName", AuditParameterKind.Text), ("email", AuditParameterKind.Text)),
            [AuditEventType.RoleCreated] = Define(RoleEntity,
                ("role", AuditParameterKind.Role), ("permissionCodes", AuditParameterKind.TextList)),
            [AuditEventType.RoleUpdated] = Define(RoleEntity,
                ("nameBefore", AuditParameterKind.Text), ("nameAfter", AuditParameterKind.Text),
                ("permissionsAdded", AuditParameterKind.TextList), ("permissionsRemoved", AuditParameterKind.TextList)),
            [AuditEventType.RoleDeleted] = Define(RoleEntity, ("role", AuditParameterKind.Role)),
            [AuditEventType.OrganizationRenamed] = Define(OrganizationEntity,
                ("nameBefore", AuditParameterKind.Text), ("nameAfter", AuditParameterKind.Text)),
            [AuditEventType.OrganizationDeactivated] = Define(OrganizationEntity),
            [AuditEventType.OrganizationReactivated] = Define(OrganizationEntity)
        };

    public static AuditEventDefinition Get(AuditEventType type) =>
        Definitions.TryGetValue(type, out AuditEventDefinition? definition)
            ? definition
            : throw new InvalidOperationException($"Audit event type {type} is not in the catalogue.");

    /// <summary>Programmer-error check: a bad event is a bug at the call site, not a user error.</summary>
    public static void Validate(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (auditEvent.OrganizationId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit event has no organization.");
        }

        AuditEventDefinition definition = Get(auditEvent.Type);
        string[] undeclared = [.. auditEvent.Parameters.Keys.Except(definition.Parameters.Keys)];
        string[] missing = [.. definition.Parameters.Keys.Except(auditEvent.Parameters.Keys)];
        if (undeclared.Length > 0 || missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Audit event {auditEvent.Type} parameters mismatch. Undeclared: [{string.Join(", ", undeclared)}]; "
                + $"missing: [{string.Join(", ", missing)}].");
        }

        foreach ((string name, AuditParameterKind kind) in definition.Parameters)
        {
            if (!HasKind(auditEvent.Parameters[name], kind))
            {
                throw new InvalidOperationException(
                    $"Audit event {auditEvent.Type} parameter '{name}' must be {kind}.");
            }
        }

        bool needsActor = auditEvent.ActorKind is ActorKind.Account or ActorKind.SystemAdministrator;
        if (needsActor != auditEvent.ActorAccountId.HasValue)
        {
            throw new InvalidOperationException(
                $"Audit event {auditEvent.Type}: actor kind {auditEvent.ActorKind} "
                + (needsActor ? "requires an actor account." : "must not carry an actor account."));
        }
    }

    /// <summary>Maps a stored row's (<c>entity_name</c>, <c>action</c>) back to its type; false for anything else.</summary>
    public static bool TryResolve(string entityName, string action, out AuditEventType type)
    {
        type = default;
        if (!Enum.TryParse(action, ignoreCase: false, out AuditEventType parsed)
            || !Enum.IsDefined(parsed)
            || !string.Equals(parsed.ToString(), action, StringComparison.Ordinal)
            || !string.Equals(Get(parsed).EntityName, entityName, StringComparison.Ordinal))
        {
            return false;
        }

        type = parsed;
        return true;
    }

    private static bool HasKind(object value, AuditParameterKind kind) => kind switch
    {
        AuditParameterKind.Text => value is string,
        AuditParameterKind.TextList => value is IReadOnlyList<string>,
        AuditParameterKind.Role => value is AuditRoleRef,
        AuditParameterKind.RoleList => value is IReadOnlyList<AuditRoleRef>,
        _ => false
    };

    private static AuditEventDefinition Invitation() => Define(InvitationEntity,
        ("email", AuditParameterKind.Text), ("role", AuditParameterKind.Role));

    private static AuditEventDefinition Define(string entityName, params (string Name, AuditParameterKind Kind)[] parameters) =>
        new(entityName, parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Kind).AsReadOnly());
}
