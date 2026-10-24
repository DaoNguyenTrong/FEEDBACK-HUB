using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Roles;

public class RoleServiceReadTests
{
    private static readonly IReadOnlyList<string> NoCodes = [];

    private static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private static RoleRow Row(
        string name, SystemRoleKind? kind, IReadOnlyList<string> codes, int holders = 0, int pendingInvitations = 0) =>
        new(Guid.NewGuid(), name, kind, codes, holders, pendingInvitations);

    private static readonly RoleRow Owner = Row("Owner", SystemRoleKind.Owner, Permissions.All, 1);

    private static readonly RoleRow Admin = Row(
        "Admin", SystemRoleKind.Admin, [Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin], 2);

    private static readonly RoleRow Member = Row("Member", SystemRoleKind.Member, NoCodes, 5);

    private sealed record Fixture(RoleService Service, IRoleQueries Queries, IRoleGrantReader Reader, Guid OrganizationId);

    private static Fixture CreateFixture(
        IReadOnlyList<RoleRow> rows, bool callerIsOwner, IReadOnlySet<string> callerPermissions, bool personalContext = false)
    {
        Guid organizationId = Guid.NewGuid();
        Guid callerId = Guid.NewGuid();

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenant = Substitute.For<ICurrentTenantProvider>();
        tenant.OrganizationId.Returns(personalContext ? null : organizationId);

        IRoleQueries queries = Substitute.For<IRoleQueries>();
        queries.ListAsync(organizationId, Now, Arg.Any<CancellationToken>()).Returns(rows);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        IRoleGrantReader reader = Substitute.For<IRoleGrantReader>();
        reader.LoadCallerAsync(organizationId, callerId, Arg.Any<CancellationToken>())
            .Returns(new RoleGrantCaller(callerId, callerIsOwner, callerPermissions));

        RoleService service = new(
            Substitute.For<IUnitOfWork>(),
            currentUser,
            tenant,
            queries,
            reader,
            Substitute.For<IOrganizationLock>(),
            Substitute.For<IAuditTrail>(),
            Substitute.For<IPermissionResolver>(),
            clock);

        return new Fixture(service, queries, reader, organizationId);
    }

    // AC-311: a caller who can only manage members is offered just the roles within their own permissions.
    [Fact]
    public async Task ListOptionsAsync_HrRoleCaller_OnlyMemberGrantable()
    {
        Fixture fixture = CreateFixture(
            [Owner, Admin, Member],
            callerIsOwner: false,
            new HashSet<string> { Permissions.OrganizationMembersManage });

        IReadOnlyList<RoleOptionDto> options = await fixture.Service.ListOptionsAsync(CancellationToken.None);

        Assert.Equal(["Owner", "Admin", "Member"], options.Select(option => option.Name));
        Assert.Equal([false, false, true], options.Select(option => option.CanGrant));
        Assert.Equal(Member.Id, options.Single(option => option.CanGrant).Id);
    }

    [Fact]
    public async Task ListOptionsAsync_Owner_AllGrantable()
    {
        Fixture fixture = CreateFixture([Owner, Admin, Member], callerIsOwner: true, Permissions.All.ToHashSet());

        IReadOnlyList<RoleOptionDto> options = await fixture.Service.ListOptionsAsync(CancellationToken.None);

        Assert.All(options, option => Assert.True(option.CanGrant));
        Assert.Equal(SystemRoleKind.Owner, options[0].SystemKind);
    }

    [Fact]
    public async Task ListOptionsAsync_PersonalContext_ThrowsAccessDenied()
    {
        Fixture fixture = CreateFixture([Member], callerIsOwner: false, new HashSet<string>(), personalContext: true);

        ForbiddenException exception =
            await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.ListOptionsAsync(CancellationToken.None));

        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, exception.Message);
    }

    // AC-248, AC-257
    [Fact]
    public async Task ListAsync_FlagsAndBlockedReasons()
    {
        RoleRow beyondCaller = Row("Beyond", null, [Permissions.OrganizationMembersManage, Permissions.FeedbackHubAdmin]);
        RoleRow withHolders = Row("Staffed", null, [Permissions.OrganizationMembersManage], holders: 3);
        RoleRow free = Row("Free", null, [Permissions.OrganizationMembersManage]);
        Fixture fixture = CreateFixture(
            [Owner, Admin, Member, beyondCaller, withHolders, free],
            callerIsOwner: false,
            new HashSet<string> { Permissions.OrganizationRolesManage, Permissions.OrganizationMembersManage });

        IReadOnlyList<RoleDto> roles = await fixture.Service.ListAsync(CancellationToken.None);

        RoleDto Find(RoleRow row) => roles.Single(role => role.Id == row.Id);

        Assert.Equal(
            [Owner.Id, Admin.Id, Member.Id, beyondCaller.Id, withHolders.Id, free.Id], roles.Select(role => role.Id));

        Assert.Equal(RoleDeleteBlockedReason.SystemRole, Find(Owner).DeleteBlockedReason);
        Assert.Equal(RoleDeleteBlockedReason.SystemRole, Find(Member).DeleteBlockedReason);
        Assert.All([Owner, Admin, Member], row => Assert.False(Find(row).CanEdit));
        Assert.All([Owner, Admin, Member], row => Assert.True(Find(row).IsSystem));

        RoleDto beyond = Find(beyondCaller);
        Assert.False(beyond.CanEdit);
        Assert.False(beyond.CanDelete);
        Assert.Equal(RoleDeleteBlockedReason.InsufficientRolePermissions, beyond.DeleteBlockedReason);

        RoleDto staffed = Find(withHolders);
        Assert.True(staffed.CanEdit);
        Assert.False(staffed.CanDelete);
        Assert.Equal(RoleDeleteBlockedReason.HasHolders, staffed.DeleteBlockedReason);
        Assert.Equal(3, staffed.HolderCount);

        RoleDto freeDto = Find(free);
        Assert.True(freeDto.CanEdit);
        Assert.True(freeDto.CanDelete);
        Assert.Null(freeDto.DeleteBlockedReason);
        Assert.Equal([Permissions.OrganizationMembersManage], freeDto.PermissionCodes);
    }

    // An unreachable role that also has holders reports the permission reason first.
    [Fact]
    public async Task ListAsync_BeyondCallerWithHolders_ReportsInsufficientPermissionsFirst()
    {
        RoleRow beyondAndStaffed = Row("Both", null, [Permissions.OrganizationRolesManage], holders: 4);
        Fixture fixture = CreateFixture([beyondAndStaffed], callerIsOwner: false, new HashSet<string>());

        IReadOnlyList<RoleDto> roles = await fixture.Service.ListAsync(CancellationToken.None);

        Assert.Equal(RoleDeleteBlockedReason.InsufficientRolePermissions, roles.Single().DeleteBlockedReason);
    }

    [Fact]
    public async Task ListAsync_ReadsCallerFromReaderNotCache()
    {
        Fixture fixture = CreateFixture([Member], callerIsOwner: true, Permissions.All.ToHashSet());

        await fixture.Service.ListAsync(CancellationToken.None);

        await fixture.Reader.Received(1).LoadCallerAsync(
            fixture.OrganizationId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // BR-75, T9: a roles manager who cannot manage members learns nothing about invitations.
    [Fact]
    public async Task ListAsync_CallerWithoutMembersManage_PendingInvitationCountNull()
    {
        RoleRow invited = Row("Invited", null, NoCodes, pendingInvitations: 3);
        Fixture fixture = CreateFixture(
            [Owner, invited], callerIsOwner: false, new HashSet<string> { Permissions.OrganizationRolesManage });

        IReadOnlyList<RoleDto> roles = await fixture.Service.ListAsync(CancellationToken.None);

        Assert.All(roles, role => Assert.Null(role.PendingInvitationCount));
    }

    // The query is stubbed for the clock's time only, so a different time would return no rows.
    [Fact]
    public async Task ListAsync_CallerWithMembersManage_PendingInvitationCountFromQuery()
    {
        RoleRow invited = Row("Invited", null, NoCodes, pendingInvitations: 3);
        Fixture fixture = CreateFixture(
            [Owner, invited],
            callerIsOwner: false,
            new HashSet<string> { Permissions.OrganizationRolesManage, Permissions.OrganizationMembersManage });

        IReadOnlyList<RoleDto> roles = await fixture.Service.ListAsync(CancellationToken.None);

        Assert.Equal([0, 3], roles.Select(role => role.PendingInvitationCount));
    }
}
