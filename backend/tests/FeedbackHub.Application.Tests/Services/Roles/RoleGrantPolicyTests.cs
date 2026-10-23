using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Tests.Services.Roles;

public class RoleGrantPolicyTests
{
    private static readonly RoleGrantRole OwnerRole = Role("Owner", SystemRoleKind.Owner, [.. Permissions.All]);

    private static readonly RoleGrantRole AdminRole = Role(
        "Admin", SystemRoleKind.Admin, Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin);

    private static RoleGrantRole Role(string name, SystemRoleKind? kind, params string[] permissions) =>
        new(Guid.NewGuid(), name, kind, permissions.ToHashSet());

    private static RoleGrantCaller Caller(bool isOwner, params string[] permissions) =>
        new(Guid.NewGuid(), isOwner, permissions.ToHashSet());

    private static RoleGrantCaller OwnerCaller() => Caller(true, [.. Permissions.All]);

    // Holds every catalog permission through a custom role, but is not an Owner (AC-340).
    private static RoleGrantCaller FullPermissionsCaller() => Caller(false, [.. Permissions.All]);

    // "Nhân sự": only the member-management permission (AC-153, AC-338).
    private static RoleGrantCaller PeopleCaller() => Caller(false, Permissions.OrganizationMembersManage);

    // CanGrant

    // PRD00-AC-155 (Admin, not Owner, is refused), PRD00-AC-157 (an Owner grants Owner)
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CanGrant_OwnerRole_OnlyForOwnerCaller(bool callerIsOwner, bool expected)
    {
        RoleGrantCaller caller = callerIsOwner ? OwnerCaller() : FullPermissionsCaller();

        Assert.Equal(expected, RoleGrantPolicy.CanGrant(caller, OwnerRole));
    }

    [Fact]
    public void CanGrant_RolePermissionsSubsetOfCaller_ReturnsTrue()
    {
        RoleGrantRole role = Role("Tuyển dụng", null, Permissions.OrganizationMembersManage);

        Assert.True(RoleGrantPolicy.CanGrant(PeopleCaller(), role));
    }

    // PRD00-AC-153, PRD00-AC-154: Admin carries feedbackhub.admin, which "Nhân sự" lacks.
    [Fact]
    public void CanGrant_RoleHasPermissionCallerLacks_ReturnsFalse()
    {
        Assert.False(RoleGrantPolicy.CanGrant(PeopleCaller(), AdminRole));
    }

    [Fact]
    public void CanGrant_RoleWithoutPermissions_ReturnsTrueForCallerWithoutPermissions()
    {
        RoleGrantRole memberRole = Role("Member", SystemRoleKind.Member);

        Assert.True(RoleGrantPolicy.CanGrant(Caller(false), memberRole));
    }

    // CanTakeAway

    // D15, PRD00-AC-340: every permission is not enough to touch an Owner's roles.
    [Fact]
    public void CanTakeAway_OwnerRoleByCallerWithAllPermissionsNotOwner_ReturnsFalse()
    {
        Assert.False(RoleGrantPolicy.CanTakeAway(FullPermissionsCaller(), OwnerRole));
    }

    [Fact]
    public void CanTakeAway_OwnerRoleByOwner_ReturnsTrue()
    {
        Assert.True(RoleGrantPolicy.CanTakeAway(OwnerCaller(), OwnerRole));
    }

    // PRD00-AC-338: "Nhân sự" cannot take Admin away.
    [Fact]
    public void CanTakeAway_RoleHasPermissionCallerLacks_ReturnsFalse()
    {
        Assert.False(RoleGrantPolicy.CanTakeAway(PeopleCaller(), AdminRole));
    }

    [Fact]
    public void CanTakeAway_RolePermissionsSubsetOfCaller_ReturnsTrue()
    {
        Assert.True(RoleGrantPolicy.CanTakeAway(FullPermissionsCaller(), AdminRole));
    }

    // CheckTakeAwayAll

    // PRD00-AC-341: the Owner reason wins even when another role is also out of reach.
    [Fact]
    public void CheckTakeAwayAll_OwnerAndLackingRole_ReturnsOwnerOnlyWithoutRole()
    {
        RoleGrantDenial? denial = RoleGrantPolicy.CheckTakeAwayAll(PeopleCaller(), [AdminRole, OwnerRole]);

        Assert.Equal(new RoleGrantDenial(RoleLockReason.OwnerOnly, null), denial);
    }

    // PRD00-AC-338: the reason names the first role out of reach, in the order given.
    [Fact]
    public void CheckTakeAwayAll_TwoLackingRoles_ReturnsFirstInGivenOrder()
    {
        RoleGrantRole member = Role("Member", SystemRoleKind.Member);
        RoleGrantRole auditor = Role("Kiểm toán", null, Permissions.AuditLogsView);

        RoleGrantDenial? denial = RoleGrantPolicy.CheckTakeAwayAll(PeopleCaller(), [member, AdminRole, auditor]);

        Assert.Equal(new RoleGrantDenial(RoleLockReason.InsufficientRolePermissions, AdminRole), denial);
    }

    // D15, PRD00-AC-340
    [Fact]
    public void CheckTakeAwayAll_OwnerRoleByCallerWithAllPermissionsNotOwner_ReturnsOwnerOnly()
    {
        RoleGrantDenial? denial = RoleGrantPolicy.CheckTakeAwayAll(FullPermissionsCaller(), [OwnerRole]);

        Assert.Equal(new RoleGrantDenial(RoleLockReason.OwnerOnly, null), denial);
    }

    [Fact]
    public void CheckTakeAwayAll_OwnerCallerAndOwnerRole_ReturnsNull()
    {
        Assert.Null(RoleGrantPolicy.CheckTakeAwayAll(OwnerCaller(), [OwnerRole, AdminRole]));
    }

    [Fact]
    public void CheckTakeAwayAll_NoRoles_ReturnsNull()
    {
        Assert.Null(RoleGrantPolicy.CheckTakeAwayAll(Caller(false), []));
    }

    // CheckGrantAll

    [Fact]
    public void CheckGrantAll_AllGrantable_ReturnsNull()
    {
        RoleGrantRole member = Role("Member", SystemRoleKind.Member);
        RoleGrantRole recruiter = Role("Tuyển dụng", null, Permissions.OrganizationMembersManage);

        Assert.Null(RoleGrantPolicy.CheckGrantAll(PeopleCaller(), [member, recruiter]));
    }

    // PRD00-AC-155
    [Fact]
    public void CheckGrantAll_OwnerRoleByNonOwner_ReturnsOwnerOnly()
    {
        RoleGrantDenial? denial = RoleGrantPolicy.CheckGrantAll(FullPermissionsCaller(), [AdminRole, OwnerRole]);

        Assert.Equal(new RoleGrantDenial(RoleLockReason.OwnerOnly, null), denial);
    }

    // PRD00-AC-154: self-promotion to Admin is refused for the missing permission.
    [Fact]
    public void CheckGrantAll_LackingRole_ReturnsInsufficientWithThatRole()
    {
        RoleGrantRole member = Role("Member", SystemRoleKind.Member);

        RoleGrantDenial? denial = RoleGrantPolicy.CheckGrantAll(PeopleCaller(), [member, AdminRole]);

        Assert.Equal(new RoleGrantDenial(RoleLockReason.InsufficientRolePermissions, AdminRole), denial);
    }

    // PRD00-AC-157
    [Fact]
    public void CheckGrantAll_OwnerCaller_ReturnsNull()
    {
        Assert.Null(RoleGrantPolicy.CheckGrantAll(OwnerCaller(), [OwnerRole, AdminRole]));
    }

    // HasAllPermissions

    // PRD00-AC-156: a role may hold only permissions the caller has.
    [Theory]
    [InlineData(new[] { Permissions.OrganizationRolesManage }, true)]
    [InlineData(new string[0], true)]
    [InlineData(new[] { Permissions.OrganizationRolesManage, Permissions.FeedbackHubAdmin }, false)]
    public void HasAllPermissions_Codes_TrueOnlyWhenCallerHasEach(string[] codes, bool expected)
    {
        RoleGrantCaller roleManager = Caller(false, Permissions.OrganizationRolesManage);

        Assert.Equal(expected, RoleGrantPolicy.HasAllPermissions(roleManager, codes));
    }

    // ErrorCode

    [Theory]
    [InlineData(RoleLockReason.OwnerOnly, ApplicationMessages.OwnerOnlyOperation)]
    [InlineData(RoleLockReason.InsufficientRolePermissions, ApplicationMessages.InsufficientRolePermissions)]
    public void ErrorCode_Reason_ReturnsMessageKey(RoleLockReason reason, string expected)
    {
        RoleGrantRole? role = reason == RoleLockReason.OwnerOnly ? null : AdminRole;

        Assert.Equal(expected, RoleGrantPolicy.ErrorCode(new RoleGrantDenial(reason, role)));
    }
}
