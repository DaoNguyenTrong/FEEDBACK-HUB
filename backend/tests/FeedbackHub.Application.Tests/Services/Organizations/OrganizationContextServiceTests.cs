using System.Linq.Expressions;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Organizations;

public class OrganizationContextServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 9, 30, 0, DateTimeKind.Utc);

    private sealed record Fixture(
        OrganizationContextService Service,
        IUnitOfWork UnitOfWork,
        IRepository<Organization, Guid> Organizations,
        IRepository<OrganizationMember, Guid> Members,
        IOrganizationLock OrganizationLock,
        IRoleGrantReader RoleGrants,
        ISystemAdministratorResolver SystemAdministrators,
        IAuditTrail AuditTrail,
        ITenantAccessService TenantAccess,
        IPermissionResolver Permissions,
        Organization Organization,
        Guid CallerId);

    private static Fixture CreateFixture(
        string organizationName = "Bảo Minh",
        bool systemAdministrator = false,
        bool personalContext = false,
        bool callerActive = true,
        bool callerManages = true)
    {
        Guid callerId = Guid.NewGuid();
        Organization organization = Organization.Create(new OrganizationParams(organizationName, "bao-minh"));

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()!(callInfo.Arg<CancellationToken>()));
        IRepository<Organization, Guid> organizations = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizations);
        organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        OrganizationMember callerMember = OrganizationMember.Create(new OrganizationMemberParams(organization.Id, callerId));
        if (!callerActive)
        {
            callerMember.Deactivate();
        }

        IRepository<OrganizationMember, Guid> members = Substitute.For<IRepository<OrganizationMember, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(members);
        RepositoryPredicateStub.StubFirstOrDefault(members, [callerMember]);

        // The caller's permissions as the database holds them after the lock, whatever the cache said.
        IRoleGrantReader roleGrants = Substitute.For<IRoleGrantReader>();
        HashSet<string> callerPermissions = callerManages ? [Permissions.OrganizationManage] : [];
        roleGrants.LoadCallerAsync(organization.Id, callerId, Arg.Any<CancellationToken>())
            .Returns(new RoleGrantCaller(callerId, false, callerPermissions));

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());
        ICurrentTenantProvider tenantProvider = Substitute.For<ICurrentTenantProvider>();
        tenantProvider.OrganizationId.Returns(personalContext ? null : organization.Id);

        ISystemAdministratorResolver systemAdministrators = Substitute.For<ISystemAdministratorResolver>();
        systemAdministrators.IsSystemAdministratorAsync(callerId, Arg.Any<CancellationToken>())
            .Returns(systemAdministrator);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        IOrganizationLock organizationLock = Substitute.For<IOrganizationLock>();
        IAuditTrail auditTrail = Substitute.For<IAuditTrail>();
        ITenantAccessService tenantAccess = Substitute.For<ITenantAccessService>();
        IPermissionResolver permissions = Substitute.For<IPermissionResolver>();

        OrganizationContextService service = new(
            unitOfWork,
            currentUser,
            tenantProvider,
            organizationLock,
            roleGrants,
            systemAdministrators,
            auditTrail,
            new OrganizationStatusChanger(unitOfWork, organizationLock, auditTrail, tenantAccess, permissions, clock));

        return new Fixture(
            service, unitOfWork, organizations, members, organizationLock, roleGrants, systemAdministrators, auditTrail,
            tenantAccess, permissions, organization, callerId);
    }

    private static async Task AssertNothingSavedOrRecordedAsync(Fixture f)
    {
        f.Organizations.DidNotReceive().Update(Arg.Any<Organization>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    private static async Task AssertNoCacheInvalidatedAsync(Fixture f)
    {
        await f.TenantAccess.DidNotReceive().InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await f.Permissions.DidNotReceive().InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // GetAsync

    [Fact]
    public async Task GetAsync_WorkingOrganization_ReturnsIt()
    {
        Fixture f = CreateFixture();

        CurrentOrganizationDto result = await f.Service.GetAsync(CancellationToken.None);

        Assert.Equal(new CurrentOrganizationDto(f.Organization.Id, "Bảo Minh", "bao-minh", true), result);
    }

    [Fact]
    public async Task GetAsync_PersonalContext_Throws403()
    {
        Fixture f = CreateFixture(personalContext: true);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied, () => f.Service.GetAsync(CancellationToken.None));

        await f.Organizations.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // RenameAsync

    // AC-367: the event carries both names; the row is locked, then the gate re-checked, before it is read.
    [Fact]
    public async Task RenameAsync_NewName_UpdatesAndRecordsRenamedWithBothNames()
    {
        Fixture f = CreateFixture();

        CurrentOrganizationDto result = await f.Service.RenameAsync(
            new RenameOrganizationRequest("  Bảo Minh Group  "), CancellationToken.None);

        Assert.Equal(new CurrentOrganizationDto(f.Organization.Id, "Bảo Minh Group", "bao-minh", true), result);
        Assert.Equal("Bảo Minh Group", f.Organization.Name);
        f.Organizations.Received(1).Update(f.Organization);
        await f.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationRenamed &&
                e.OrganizationId == f.Organization.Id &&
                e.ActorKind == ActorKind.Account &&
                e.ActorAccountId == f.CallerId &&
                (string)e.Parameters["nameBefore"] == "Bảo Minh" &&
                (string)e.Parameters["nameAfter"] == "Bảo Minh Group"),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Members.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
            f.Organizations.GetByIdAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.RoleGrants.LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
            f.Organizations.GetByIdAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RenameAsync_SameNameAfterTrim_NoSaveNoRecord()
    {
        Fixture f = CreateFixture();

        CurrentOrganizationDto result = await f.Service.RenameAsync(
            new RenameOrganizationRequest("  Bảo Minh "), CancellationToken.None);

        Assert.Equal(new CurrentOrganizationDto(f.Organization.Id, "Bảo Minh", "bao-minh", true), result);
        await AssertNothingSavedOrRecordedAsync(f);
    }

    [Fact]
    public async Task RenameAsync_CaseOnlyChange_UpdatesAndRecordsRenamed()
    {
        Fixture f = CreateFixture();

        CurrentOrganizationDto result = await f.Service.RenameAsync(
            new RenameOrganizationRequest("BẢO MINH"), CancellationToken.None);

        Assert.Equal("BẢO MINH", result.Name);
        f.Organizations.Received(1).Update(f.Organization);
        await f.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationRenamed &&
                (string)e.Parameters["nameBefore"] == "Bảo Minh" &&
                (string)e.Parameters["nameAfter"] == "BẢO MINH"),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // Deactivated between the gate and the lock: the rename is refused like the gate would, and nothing is written.
    [Fact]
    public async Task RenameAsync_DeactivatedAfterGate_Throws403SuspendedNothingSaved()
    {
        Fixture f = CreateFixture();
        f.Organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, Now));

        ForbiddenException exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.RenameAsync(new RenameOrganizationRequest("Bảo Minh Group"), CancellationToken.None));

        Assert.Equal(ApplicationMessages.OrganizationSuspended, exception.Message);
        Assert.Equal("Bảo Minh", exception.Extensions[ProblemExtensionKeys.OrganizationName]);
        Assert.Equal("Bảo Minh", f.Organization.Name);
        await f.OrganizationLock.Received(1).AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        await AssertNothingSavedOrRecordedAsync(f);
    }

    // Removed between the gate and the lock: refused the way the gate now would, nothing written.
    [Fact]
    public async Task RenameAsync_CallerRemovedWhileWaitingForLock_Throws403AccessDeniedNothingSaved()
    {
        Fixture f = CreateFixture(callerActive: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.RenameAsync(new RenameOrganizationRequest("Bảo Minh Group"), CancellationToken.None));

        Assert.Equal("Bảo Minh", f.Organization.Name);
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Members.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
        });
        await AssertNothingSavedOrRecordedAsync(f);
    }

    // organizations.manage taken away while the cache still granted it: re-read after the gate, nothing written.
    [Fact]
    public async Task RenameAsync_CallerLostOrganizationsManageInDatabase_Throws403AccessDeniedNothingSaved()
    {
        Fixture f = CreateFixture(callerManages: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.RenameAsync(new RenameOrganizationRequest("Bảo Minh Group"), CancellationToken.None));

        Assert.Equal("Bảo Minh", f.Organization.Name);
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Members.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>());
            f.Organizations.GetByIdAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.RoleGrants.LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
        });
        await AssertNothingSavedOrRecordedAsync(f);
    }

    // AC-187: a blank name is refused before the current name is even read.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameAsync_BlankName_Throws400BeforeComparison(string? name)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            DomainMessages.OrganizationNameRequired,
            () => f.Service.RenameAsync(new RenameOrganizationRequest(name), CancellationToken.None));

        await f.Organizations.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await AssertNothingSavedOrRecordedAsync(f);
    }

    // DeactivateAsync

    // AC-147, BR-54
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateAsync_SystemAdministratorMemberWithoutReason_Throws400NothingSaved(string? reason)
    {
        Fixture f = CreateFixture(systemAdministrator: true);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.DeactivationReasonRequired,
            () => f.Service.DeactivateAsync(new DeactivateOrganizationRequest(reason), CancellationToken.None));

        Assert.True(f.Organization.Status);
        await f.OrganizationLock.DidNotReceive().AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await AssertNothingSavedOrRecordedAsync(f);
        await AssertNoCacheInvalidatedAsync(f);
    }

    // AC-148, AC-369: a member acts as a member, even one who is also a system administrator.
    [Fact]
    public async Task DeactivateAsync_SystemAdministratorMemberWithReason_RecordsAccountActor()
    {
        Fixture f = CreateFixture(systemAdministrator: true);

        await f.Service.DeactivateAsync(new DeactivateOrganizationRequest(" Hết hợp đồng "), CancellationToken.None);

        Assert.False(f.Organization.Status);
        Assert.False(f.Organization.DeactivatedBySystemAdministrator);
        Assert.Equal("Hết hợp đồng", f.Organization.DeactivationReason);
        await f.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationDeactivated &&
                e.ActorKind == ActorKind.Account &&
                e.ActorAccountId == f.CallerId &&
                e.Parameters.Count == 0),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // AC-141, AC-146, AC-183
    [Fact]
    public async Task DeactivateAsync_ManagerWithoutReason_DeactivatesAndRecords()
    {
        Fixture f = CreateFixture();

        await f.Service.DeactivateAsync(new DeactivateOrganizationRequest(null), CancellationToken.None);

        Assert.False(f.Organization.Status);
        Assert.Equal(f.CallerId, f.Organization.DeactivatedByAccountId);
        Assert.Equal(Now, f.Organization.DeactivatedAt);
        Assert.False(f.Organization.DeactivatedBySystemAdministrator);
        Assert.Null(f.Organization.DeactivationReason);
        f.Organizations.Received(1).Update(f.Organization);
        await f.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationDeactivated &&
                e.OrganizationId == f.Organization.Id &&
                e.ActorKind == ActorKind.Account &&
                e.ActorAccountId == f.CallerId),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.RoleGrants.LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            f.TenantAccess.InvalidateOrganizationAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.Permissions.InvalidateOrganizationAsync(f.Organization.Id, Arg.Any<CancellationToken>());
        });
    }

    // organizations.manage taken away while the cache still granted it: re-read after the lock, nothing changes.
    [Fact]
    public async Task DeactivateAsync_CallerLostOrganizationsManageInDatabase_Throws403AccessDeniedNothingSaved()
    {
        Fixture f = CreateFixture(callerManages: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.DeactivateAsync(new DeactivateOrganizationRequest("Hết hợp đồng"), CancellationToken.None));

        Assert.True(f.Organization.Status);
        Assert.Null(f.Organization.DeactivatedAt);
        Received.InOrder(() =>
        {
            f.OrganizationLock.AcquireAsync(f.Organization.Id, Arg.Any<CancellationToken>());
            f.RoleGrants.LoadCallerAsync(f.Organization.Id, f.CallerId, Arg.Any<CancellationToken>());
        });
        await AssertNothingSavedOrRecordedAsync(f);
        await AssertNoCacheInvalidatedAsync(f);
    }
}
