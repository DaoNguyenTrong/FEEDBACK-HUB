using System.Runtime.CompilerServices;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Tests.Services.AuditTrail;

public class AuditEventCatalogTests
{
    private static Organization NewOrganization() => Organization.Create(new OrganizationParams("Acme", "acme"));

    private static AuditEvent Event(
        AuditEventType type,
        Dictionary<string, object>? parameters,
        ActorKind actorKind = ActorKind.Account,
        Guid? actor = null) =>
        AuditEvent.For(NewOrganization(), type, actorKind, actorKind == ActorKind.System ? null : actor ?? Guid.NewGuid(),
            parameters);

    private static AuditRoleRef Role() => new(Guid.NewGuid(), "Admin", SystemRoleKind.Admin);

    private static Dictionary<string, object> Renamed() => new() { ["nameBefore"] = "A", ["nameAfter"] = "B" };

    [Fact]
    public void Get_EveryEnumValue_HasDefinition()
    {
        foreach (AuditEventType type in Enum.GetValues<AuditEventType>())
        {
            Assert.NotNull(AuditEventCatalog.Get(type));
        }
    }

    // TECH00 §3.7 — "name:Kind" pairs.
    [Theory]
    [InlineData(AuditEventType.InvitationSent, "OrganizationInvitation", "email:Text,role:Role")]
    [InlineData(AuditEventType.InvitationRevoked, "OrganizationInvitation", "email:Text,role:Role")]
    [InlineData(AuditEventType.InvitationDeleted, "OrganizationInvitation", "email:Text,role:Role")]
    [InlineData(AuditEventType.MemberJoined, "OrganizationMember", "accountName:Text,email:Text,role:Role")]
    [InlineData(AuditEventType.MemberRolesChanged, "OrganizationMember",
        "accountName:Text,email:Text,rolesBefore:RoleList,rolesAfter:RoleList")]
    [InlineData(AuditEventType.MemberRemoved, "OrganizationMember", "accountName:Text,email:Text,roles:RoleList")]
    [InlineData(AuditEventType.MemberLeft, "OrganizationMember", "accountName:Text,email:Text")]
    [InlineData(AuditEventType.RoleCreated, "Role", "role:Role,permissionCodes:TextList")]
    [InlineData(AuditEventType.RoleUpdated, "Role",
        "nameBefore:Text,nameAfter:Text,permissionsAdded:TextList,permissionsRemoved:TextList")]
    [InlineData(AuditEventType.RoleDeleted, "Role", "role:Role")]
    [InlineData(AuditEventType.OrganizationRenamed, "Organization", "nameBefore:Text,nameAfter:Text")]
    [InlineData(AuditEventType.OrganizationDeactivated, "Organization", "")]
    [InlineData(AuditEventType.OrganizationReactivated, "Organization", "")]
    public void Get_Definitions_MatchTech00Catalogue(AuditEventType type, string entityName, string parameters)
    {
        AuditEventDefinition definition = AuditEventCatalog.Get(type);

        Assert.Equal(entityName, definition.EntityName);
        Dictionary<string, AuditParameterKind> expected = parameters.Length == 0
            ? []
            : parameters.Split(',').Select(pair => pair.Split(':'))
                .ToDictionary(pair => pair[0], pair => Enum.Parse<AuditParameterKind>(pair[1]));
        Assert.Equal(expected.OrderBy(pair => pair.Key), definition.Parameters.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void Get_Catalogue_HasThirteenTypes() => Assert.Equal(13, Enum.GetValues<AuditEventType>().Length);

    [Fact]
    public void Validate_UndeclaredParameter_Throws()
    {
        Dictionary<string, object> parameters = Renamed();
        parameters["reason"] = "x";

        Assert.Throws<InvalidOperationException>(() =>
            AuditEventCatalog.Validate(Event(AuditEventType.OrganizationRenamed, parameters)));
    }

    [Fact]
    public void Validate_MissingDeclaredParameter_Throws() =>
        Assert.Throws<InvalidOperationException>(() => AuditEventCatalog.Validate(
            Event(AuditEventType.OrganizationRenamed, new Dictionary<string, object> { ["nameBefore"] = "A" })));

    [Fact]
    public void Validate_WrongValueKind_Throws() =>
        Assert.Throws<InvalidOperationException>(() => AuditEventCatalog.Validate(Event(
            AuditEventType.RoleDeleted, new Dictionary<string, object> { ["role"] = "Admin" })));

    [Fact]
    public void Validate_AccountOrSystemAdministratorWithoutActor_Throws()
    {
        foreach (ActorKind kind in new[] { ActorKind.Account, ActorKind.SystemAdministrator })
        {
            AuditEvent auditEvent = AuditEvent.For(NewOrganization(), AuditEventType.OrganizationDeactivated, kind, null);

            Assert.Throws<InvalidOperationException>(() => AuditEventCatalog.Validate(auditEvent));
        }
    }

    [Fact]
    public void Validate_SystemWithActor_Throws()
    {
        AuditEvent auditEvent = AuditEvent.For(
            NewOrganization(), AuditEventType.OrganizationDeactivated, ActorKind.System, Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => AuditEventCatalog.Validate(auditEvent));
    }

    [Fact]
    public void Validate_EmptyOrganizationId_Throws()
    {
        // AuditEvent has no public constructor and For rejects Guid.Empty, so build the invalid state by hand.
        AuditEvent auditEvent = (AuditEvent)RuntimeHelpers.GetUninitializedObject(typeof(AuditEvent));
        SetInit(auditEvent, nameof(AuditEvent.SubjectId), Guid.NewGuid());
        SetInit(auditEvent, nameof(AuditEvent.Type), AuditEventType.OrganizationDeactivated);
        SetInit(auditEvent, nameof(AuditEvent.ActorKind), ActorKind.System);
        SetInit(auditEvent, nameof(AuditEvent.Parameters), new Dictionary<string, object>());

        Assert.Equal(Guid.Empty, auditEvent.OrganizationId);
        Assert.Throws<InvalidOperationException>(() => AuditEventCatalog.Validate(auditEvent));
    }

    [Fact]
    public void Validate_ValidEvent_DoesNotThrow()
    {
        Guid actor = Guid.NewGuid();
        AuditEvent roleDeleted = Event(AuditEventType.RoleDeleted, new() { ["role"] = Role() }, actor: actor);
        AuditEvent changed = Event(AuditEventType.MemberRolesChanged, new()
        {
            ["accountName"] = "An",
            ["email"] = "an@example.com",
            ["rolesBefore"] = (IReadOnlyList<AuditRoleRef>)[Role()],
            ["rolesAfter"] = (IReadOnlyList<AuditRoleRef>)[],
        });
        AuditEvent created = Event(AuditEventType.RoleCreated, new()
        {
            ["role"] = Role(),
            ["permissionCodes"] = (IReadOnlyList<string>)["a.b"],
        }, ActorKind.SystemAdministrator);
        AuditEvent system = Event(AuditEventType.OrganizationDeactivated, null, ActorKind.System);

        foreach (AuditEvent auditEvent in new[] { roleDeleted, changed, created, system })
        {
            AuditEventCatalog.Validate(auditEvent);
        }
    }

    [Fact]
    public void Validate_Null_Throws() => Assert.Throws<ArgumentNullException>(() => AuditEventCatalog.Validate(null!));

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void TryResolve_StoredFormOfEveryType_ReturnsSameType(AuditEventType type)
    {
        bool found = AuditEventCatalog.TryResolve(AuditEventCatalog.Get(type).EntityName, type.ToString(), out AuditEventType resolved);

        Assert.True(found);
        Assert.Equal(type, resolved);
    }

    [Theory]
    [InlineData("OrganizationInvitation", "Revoked")]
    [InlineData("OrganizationMember", "Left")]
    [InlineData("Organization", "1")]
    [InlineData("OrganizationInvitation", "invitationsent")]
    [InlineData("Role", "InvitationSent")]
    public void TryResolve_UnknownActionWrongEntityOrNumeric_ReturnsFalse(string entityName, string action)
    {
        Assert.False(AuditEventCatalog.TryResolve(entityName, action, out _));
    }

    public static TheoryData<AuditEventType> AllTypes() => [.. Enum.GetValues<AuditEventType>()];

    private static void SetInit(AuditEvent target, string property, object value) =>
        typeof(AuditEvent).GetProperty(property)!.GetSetMethod(nonPublic: true)!.Invoke(target, [value]);
}
