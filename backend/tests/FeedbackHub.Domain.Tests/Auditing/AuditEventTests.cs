using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Domain.Tests.Auditing;

public class AuditEventTests
{
    private static Organization NewOrganization() => Organization.Create(new OrganizationParams("Acme", "acme"));

    [Fact]
    public void For_Subject_CopiesSubjectOrganizationAndId()
    {
        Guid organizationId = Guid.NewGuid();
        ApiKey apiKey = ApiKey.Create(new ApiKeyParams("Key"), "prefix", "hash", organizationId);
        Guid actor = Guid.NewGuid();

        AuditEvent auditEvent = AuditEvent.For(apiKey, AuditEventType.RoleCreated, ActorKind.Account, actor);

        Assert.Equal(organizationId, auditEvent.OrganizationId);
        Assert.Equal(apiKey.Id, auditEvent.SubjectId);
        Assert.Equal(AuditEventType.RoleCreated, auditEvent.Type);
        Assert.Equal(ActorKind.Account, auditEvent.ActorKind);
        Assert.Equal(actor, auditEvent.ActorAccountId);
    }

    [Fact]
    public void For_Organization_CopiesOrganizationIdIntoBoth()
    {
        Organization organization = NewOrganization();

        AuditEvent auditEvent = AuditEvent.For(
            organization, AuditEventType.OrganizationDeactivated, ActorKind.System, null);

        Assert.Equal(organization.Id, auditEvent.OrganizationId);
        Assert.Equal(organization.Id, auditEvent.SubjectId);
        Assert.Null(auditEvent.ActorAccountId);
    }

    [Fact]
    public void For_SubjectWithEmptyOrganizationId_Throws()
    {
        ApiKey apiKey = ApiKey.Create(new ApiKeyParams("Key"), "prefix", "hash", Guid.Empty);

        Assert.Throws<ArgumentException>(() =>
            AuditEvent.For(apiKey, AuditEventType.RoleCreated, ActorKind.Account, Guid.NewGuid()));
    }

    [Fact]
    public void For_NullParameters_IsEmptyAndCallerCannotMutate()
    {
        Organization organization = NewOrganization();
        AuditEvent withNull = AuditEvent.For(organization, AuditEventType.OrganizationDeactivated, ActorKind.System, null);
        Assert.Empty(withNull.Parameters);

        List<string> codes = ["a.b"];
        List<AuditRoleRef> roles = [new AuditRoleRef(Guid.NewGuid(), "Admin", null)];
        Dictionary<string, object> source = new()
        {
            ["nameAfter"] = "B",
            ["permissionCodes"] = codes,
            ["roles"] = roles,
        };
        AuditEvent auditEvent = AuditEvent.For(
            organization, AuditEventType.OrganizationRenamed, ActorKind.Account, Guid.NewGuid(), source);

        source["nameAfter"] = "changed";
        source["extra"] = "x";
        codes.Add("c.d");
        roles.Clear();

        Assert.Equal(3, auditEvent.Parameters.Count);
        Assert.Equal("B", auditEvent.Parameters["nameAfter"]);
        Assert.Equal(["a.b"], (IReadOnlyList<string>)auditEvent.Parameters["permissionCodes"]);
        Assert.Single((IReadOnlyList<AuditRoleRef>)auditEvent.Parameters["roles"]);
        Assert.False(auditEvent.Parameters is Dictionary<string, object>);
    }
}
