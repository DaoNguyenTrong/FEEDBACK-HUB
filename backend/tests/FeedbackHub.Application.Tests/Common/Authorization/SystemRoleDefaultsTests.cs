using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Tests.Common.Authorization;

public class SystemRoleDefaultsTests
{
    [Fact]
    public void PermissionsByKind_Admin_ContainsMembersManageAndFeedbackHubAdmin()
    {
        // PRD00-FR-23, BR-60
        Assert.Equal(
            [Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin],
            SystemRoleDefaults.PermissionsByKind[SystemRoleKind.Admin]);
    }

    [Fact]
    public void All_ContainsFeedbackHubAdmin()
    {
        Assert.Contains("feedbackhub.admin", Permissions.All);
    }
}
