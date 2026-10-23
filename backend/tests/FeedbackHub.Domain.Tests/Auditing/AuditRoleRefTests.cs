using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Domain.Tests.Auditing;

public class AuditRoleRefTests
{
    [Theory]
    [InlineData(SystemRoleKind.Admin)]
    [InlineData(null)]
    public void From_Role_CopiesIdNameAndSystemKind(SystemRoleKind? kind)
    {
        Role role = Role.Create(new RoleParams(Guid.NewGuid(), "Reviewers", kind));

        AuditRoleRef reference = AuditRoleRef.From(role);

        Assert.Equal(role.Id, reference.Id);
        Assert.Equal("Reviewers", reference.Name);
        Assert.Equal(kind, reference.SystemKind);
    }
}
