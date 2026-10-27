using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Organizations;

public class MemberServiceListTests
{
    private static readonly PageRequest Page = new(1, 50);
    private static readonly IReadOnlySet<string> Manage = new HashSet<string> { Permissions.OrganizationMembersManage };

    private static readonly RoleGrantRole OwnerRole =
        new(Guid.NewGuid(), "Owner", SystemRoleKind.Owner, Permissions.All.ToHashSet());

    private static readonly RoleGrantRole AdminRole = new(
        Guid.NewGuid(),
        "Admin",
        SystemRoleKind.Admin,
        new HashSet<string> { Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin });

    private static readonly RoleGrantRole MemberRole =
        new(Guid.NewGuid(), "Member", SystemRoleKind.Member, new HashSet<string>());

    private sealed record Fixture(
        MemberService Service,
        IOrganizationMemberQueries Queries,
        IRoleGrantReader Reader,
        Guid OrganizationId,
        Guid CallerId);

    private static Fixture CreateFixture(
        IReadOnlySet<string> callerPermissions, bool callerIsOwner, IReadOnlyList<(MemberRow Row, RoleGrantRole[] Roles)> rows,
        bool personalContext = false)
    {
        Guid organizationId = Guid.NewGuid();
        Guid callerId = rows.First().Row.AccountId;

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(personalContext ? null : organizationId);

        IOrganizationMemberQueries queries = Substitute.For<IOrganizationMemberQueries>();
        queries.ListActiveAsync(organizationId, Page, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<MemberRow>(rows.Select(r => r.Row).ToList(), 123, 1, 50));

        IRoleGrantReader reader = Substitute.For<IRoleGrantReader>();
        reader.LoadCallerAsync(organizationId, callerId, Arg.Any<CancellationToken>())
            .Returns(new RoleGrantCaller(callerId, callerIsOwner, callerPermissions));
        reader.LoadRolesOfMembersAsync(
                organizationId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(rows.ToDictionary(r => r.Row.MemberId, r => (IReadOnlyList<RoleGrantRole>)r.Roles));

        MemberService service = new(
            currentUser,
            tenant,
            queries,
            reader,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IOrganizationLock>(),
            Substitute.For<IAuditTrail>(),
            Substitute.For<ITenantAccessService>(),
            Substitute.For<IPermissionResolver>());

        return new Fixture(service, queries, reader, organizationId, callerId);
    }

    private static MemberRow Row(string name, Guid? accountId = null) =>
        new(Guid.NewGuid(), accountId ?? Guid.NewGuid(), name, $"{name.Replace(' ', '.')}@example.com".ToLowerInvariant());

    // AC-261, AC-262
    [Fact]
    public async Task ListAsync_PlainMember_SeesOnlyOwnEmailAndNoActions()
    {
        MemberRow self = Row("Bình");
        MemberRow other = Row("An");
        Fixture f = CreateFixture(
            new HashSet<string>(), false, [(self, [MemberRole]), (other, [OwnerRole, AdminRole])]);

        PagedResult<OrganizationMemberDto> result = await f.Service.ListAsync(Page, CancellationToken.None);

        OrganizationMemberDto own = result.Items[0];
        OrganizationMemberDto theirs = result.Items[1];
        Assert.Equal(self.Email, own.Email);
        Assert.True(own.IsSelf);
        Assert.Null(theirs.Email);
        Assert.False(theirs.IsSelf);
        Assert.All(result.Items, item =>
        {
            Assert.False(item.CanChangeRoles);
            Assert.False(item.CanRemove);
            Assert.Null(item.LockReason);
            Assert.Null(item.LockRole);
        });
        Assert.Equal(["Owner", "Admin"], theirs.RoleNames);
    }

    // AC-263
    [Fact]
    public async Task ListAsync_Manager_SeesEveryEmail()
    {
        MemberRow self = Row("Bình");
        MemberRow other = Row("An");
        Fixture f = CreateFixture(Manage, false, [(self, [MemberRole]), (other, [MemberRole])]);

        PagedResult<OrganizationMemberDto> result = await f.Service.ListAsync(Page, CancellationToken.None);

        Assert.Equal([self.Email, other.Email], result.Items.Select(item => item.Email));
    }

    // AC-339
    [Fact]
    public async Task ListAsync_ManagerOwnRow_CanChangeRolesNotRemove()
    {
        MemberRow self = Row("Bình");
        Fixture f = CreateFixture(Manage, false, [(self, [AdminRole])]);

        OrganizationMemberDto own = (await f.Service.ListAsync(Page, CancellationToken.None)).Items.Single();

        Assert.True(own.IsSelf);
        Assert.True(own.CanChangeRoles);
        Assert.False(own.CanRemove);
        Assert.Null(own.LockReason);
        Assert.Null(own.LockRole);
    }

    // AC-338
    [Fact]
    public async Task ListAsync_ManagerLacksAdminPermissions_RowLockedWithAdminRole()
    {
        MemberRow self = Row("Bình");
        MemberRow admin = Row("An");
        Fixture f = CreateFixture(Manage, false, [(self, [MemberRole]), (admin, [MemberRole, AdminRole])]);

        OrganizationMemberDto row = (await f.Service.ListAsync(Page, CancellationToken.None)).Items[1];

        Assert.False(row.CanChangeRoles);
        Assert.False(row.CanRemove);
        Assert.Equal(RoleLockReason.InsufficientRolePermissions, row.LockReason);
        Assert.Equal(new RoleRefDto(AdminRole.Id, "Admin", SystemRoleKind.Admin), row.LockRole);
    }

    // AC-341
    [Fact]
    public async Task ListAsync_AdminViewingOwnerRow_OwnerOnlyEvenIfAlsoLacking()
    {
        MemberRow self = Row("Bình");
        MemberRow owner = Row("An");
        Fixture f = CreateFixture(AdminRole.Permissions, false, [(self, [AdminRole]), (owner, [OwnerRole])]);

        OrganizationMemberDto row = (await f.Service.ListAsync(Page, CancellationToken.None)).Items[1];

        Assert.False(row.CanChangeRoles);
        Assert.False(row.CanRemove);
        Assert.Equal(RoleLockReason.OwnerOnly, row.LockReason);
        Assert.Null(row.LockRole);
    }

    [Fact]
    public async Task ListAsync_OwnerViewingAdminRow_CanChangeAndRemove()
    {
        MemberRow self = Row("Bình");
        MemberRow admin = Row("An");
        Fixture f = CreateFixture(OwnerRole.Permissions, true, [(self, [OwnerRole]), (admin, [AdminRole])]);

        OrganizationMemberDto row = (await f.Service.ListAsync(Page, CancellationToken.None)).Items[1];

        Assert.True(row.CanChangeRoles);
        Assert.True(row.CanRemove);
        Assert.Null(row.LockReason);
    }

    // AC-265
    [Fact]
    public async Task ListAsync_NewOrganization_OnlyOwner()
    {
        MemberRow self = Row("Bình");
        Fixture f = CreateFixture(OwnerRole.Permissions, true, [(self, [OwnerRole])]);

        PagedResult<OrganizationMemberDto> result = await f.Service.ListAsync(Page, CancellationToken.None);

        OrganizationMemberDto only = Assert.Single(result.Items);
        Assert.Equal(self.AccountId, only.AccountId);
        Assert.Equal([OwnerRole.Id], only.RoleIds);
        Assert.Equal(new RoleRefDto(OwnerRole.Id, "Owner", SystemRoleKind.Owner), Assert.Single(only.Roles));
    }

    [Fact]
    public async Task ListAsync_Page_LoadsRolesOfAllRowsInOneCallAndKeepsPaging()
    {
        MemberRow self = Row("Bình");
        MemberRow other = Row("An");
        Fixture f = CreateFixture(Manage, false, [(self, [MemberRole]), (other, [MemberRole])]);

        PagedResult<OrganizationMemberDto> result = await f.Service.ListAsync(Page, CancellationToken.None);

        await f.Reader.Received(1).LoadRolesOfMembersAsync(
            f.OrganizationId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids != null && ids.SequenceEqual(new[] { self.MemberId, other.MemberId })),
            Arg.Any<CancellationToken>());
        Assert.Equal((123, 1, 50), (result.TotalCount, result.PageNumber, result.PageSize));
    }

    [Fact]
    public async Task ListAsync_MemberWithoutRoles_HasEmptyRoleLists()
    {
        MemberRow self = Row("Bình");
        Fixture f = CreateFixture(Manage, false, [(self, [])]);

        OrganizationMemberDto row = (await f.Service.ListAsync(Page, CancellationToken.None)).Items.Single();

        Assert.Empty(row.RoleIds);
        Assert.Empty(row.RoleNames);
        Assert.Empty(row.Roles);
    }

    [Fact]
    public async Task ListAsync_PersonalContext_Throws403AccessDenied()
    {
        Fixture f = CreateFixture(Manage, false, [(Row("Bình"), [])], personalContext: true);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.Service.ListAsync(Page, CancellationToken.None));
        await f.Queries.DidNotReceiveWithAnyArgs().ListActiveAsync(default, default!, default);
    }
}
