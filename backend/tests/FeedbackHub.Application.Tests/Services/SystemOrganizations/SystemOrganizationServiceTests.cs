using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.SystemOrganizations;

public class SystemOrganizationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 9, 30, 0, DateTimeKind.Utc);

    private sealed record Fixture(
        SystemOrganizationService Service,
        IUnitOfWork UnitOfWork,
        IRepository<Organization, Guid> Organizations,
        ISystemOrganizationQueries Queries,
        IAuditTrail AuditTrail,
        ITenantAccessService TenantAccess,
        IPermissionResolver Permissions,
        List<string> Timeline,
        Guid CallerId);

    private static Fixture CreateFixture(Organization? organization = null, bool callerIsMember = false)
    {
        Guid callerId = Guid.NewGuid();
        List<string> timeline = [];

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                Func<CancellationToken, Task> operation = callInfo.Arg<Func<CancellationToken, Task>>()!;
                await operation(callInfo.Arg<CancellationToken>());
                timeline.Add("commit");
            });
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            timeline.Add("save");
            return 1;
        });

        IRepository<Organization, Guid> organizations = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizations);
        if (organization is not null)
        {
            organizations.GetByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        }

        IRepository<OrganizationMember, Guid> memberRepository = Substitute.For<IRepository<OrganizationMember, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepository);
        OrganizationMember[] members = callerIsMember && organization is not null
            ? [OrganizationMember.Create(new OrganizationMemberParams(organization.Id, callerId))]
            : [];
        RepositoryPredicateStub.StubFirstOrDefault(memberRepository, members);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(callerId.ToString());

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        ISystemOrganizationQueries queries = Substitute.For<ISystemOrganizationQueries>();
        IAuditTrail auditTrail = Substitute.For<IAuditTrail>();

        ITenantAccessService tenantAccess = Substitute.For<ITenantAccessService>();
        tenantAccess.InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                timeline.Add("invalidate-tenant-access");
                return Task.CompletedTask;
            });

        IPermissionResolver permissions = Substitute.For<IPermissionResolver>();
        permissions.InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                timeline.Add("invalidate-permissions");
                return Task.CompletedTask;
            });

        SystemOrganizationService service = new(
            unitOfWork,
            currentUser,
            queries,
            new SystemAdministratorActor(unitOfWork),
            auditTrail,
            tenantAccess,
            permissions,
            clock);

        return new Fixture(
            service, unitOfWork, organizations, queries, auditTrail, tenantAccess, permissions, timeline, callerId);
    }

    private static Organization NewOrganization() =>
        Organization.Create(new OrganizationParams("Bảo Minh", $"bao-minh-{Guid.NewGuid():N}"));

    private static Organization DeactivatedOrganization()
    {
        Organization organization = NewOrganization();
        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), true, "Hết hợp đồng", Now.AddDays(-1)));
        return organization;
    }

    // ListAsync

    [Fact]
    public async Task ListAsync_Always_ReturnsThePageFromTheQueries()
    {
        Fixture fixture = CreateFixture();
        PageRequest request = new(2, 20);
        PagedResult<SystemOrganizationDto> page = new([new SystemOrganizationDto(Guid.NewGuid(), "An Bình", 3, false)], 21, 2, 20);
        fixture.Queries.ListAsync(request, Arg.Any<CancellationToken>()).Returns(page);

        PagedResult<SystemOrganizationDto> result = await fixture.Service.ListAsync(request, CancellationToken.None);

        Assert.Same(page, result);
        await fixture.Queries.Received(1).ListAsync(request, Arg.Any<CancellationToken>());
    }

    // DeactivateAsync

    // AC-138, AC-139
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateAsync_BlankReason_Throws400AndLoadsNothing(string? reason)
    {
        Organization organization = NewOrganization();
        Fixture fixture = CreateFixture(organization);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.DeactivationReasonRequired,
            () => fixture.Service.DeactivateAsync(
                organization.Id, new DeactivateOrganizationRequest(reason), CancellationToken.None));

        Assert.True(organization.Status);
        await fixture.Organizations.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
        await fixture.TenantAccess.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_UnknownOrganization_Throws404()
    {
        Fixture fixture = CreateFixture();
        Guid unknownId = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Organization>(
            unknownId,
            () => fixture.Service.DeactivateAsync(
                unknownId, new DeactivateOrganizationRequest("Hết hợp đồng"), CancellationToken.None));

        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
        await fixture.TenantAccess.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await fixture.Permissions.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-368: an administrator acting from outside the organization is recorded as such, and the flag says so.
    [Fact]
    public async Task DeactivateAsync_NotMember_RecordsSystemAdministratorAndFlagsOrganization()
    {
        Organization organization = NewOrganization();
        Fixture fixture = CreateFixture(organization);

        await fixture.Service.DeactivateAsync(
            organization.Id, new DeactivateOrganizationRequest("  Hết hợp đồng  "), CancellationToken.None);

        Assert.False(organization.Status);
        Assert.True(organization.DeactivatedBySystemAdministrator);
        Assert.Equal(fixture.CallerId, organization.DeactivatedByAccountId);
        Assert.Equal(Now, organization.DeactivatedAt);
        Assert.Equal("Hết hợp đồng", organization.DeactivationReason);
        fixture.Organizations.Received(1).Update(organization);
        await fixture.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationDeactivated &&
                e.OrganizationId == organization.Id &&
                e.SubjectId == organization.Id &&
                e.ActorKind == ActorKind.SystemAdministrator &&
                e.ActorAccountId == fixture.CallerId &&
                e.Parameters.Count == 0),
            Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // AC-375: a system administrator who is also an active member acts as that member.
    [Fact]
    public async Task DeactivateAsync_ActiveMember_RecordsAccountAndFlagFalse()
    {
        Organization organization = NewOrganization();
        Fixture fixture = CreateFixture(organization, callerIsMember: true);

        await fixture.Service.DeactivateAsync(
            organization.Id, new DeactivateOrganizationRequest("Hết hợp đồng"), CancellationToken.None);

        Assert.False(organization.Status);
        Assert.False(organization.DeactivatedBySystemAdministrator);
        await fixture.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationDeactivated &&
                e.ActorKind == ActorKind.Account &&
                e.ActorAccountId == fixture.CallerId),
            Arg.Any<CancellationToken>());
        await fixture.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyDeactivated_NoSaveNoRecordNoInvalidate()
    {
        Organization organization = DeactivatedOrganization();
        Fixture fixture = CreateFixture(organization);

        await fixture.Service.DeactivateAsync(
            organization.Id, new DeactivateOrganizationRequest("Lý do khác"), CancellationToken.None);

        Assert.Equal("Hết hợp đồng", organization.DeactivationReason);
        fixture.Organizations.DidNotReceive().Update(Arg.Any<Organization>());
        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
        await fixture.TenantAccess.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await fixture.Permissions.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-183: both caches drop the organization, and only once the change is committed.
    [Fact]
    public async Task DeactivateAsync_Saves_ThenInvalidatesTenantAccessAndPermissions()
    {
        Organization organization = NewOrganization();
        Fixture fixture = CreateFixture(organization);

        await fixture.Service.DeactivateAsync(
            organization.Id, new DeactivateOrganizationRequest("Hết hợp đồng"), CancellationToken.None);

        Received.InOrder(() =>
        {
            fixture.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            fixture.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            fixture.TenantAccess.InvalidateOrganizationAsync(organization.Id, Arg.Any<CancellationToken>());
            fixture.Permissions.InvalidateOrganizationAsync(organization.Id, Arg.Any<CancellationToken>());
        });
        Assert.Equal(["save", "commit", "invalidate-tenant-access", "invalidate-permissions"], fixture.Timeline);
    }

    // ReactivateAsync

    [Fact]
    public async Task ReactivateAsync_Active_NoRecord()
    {
        Organization organization = NewOrganization();
        Fixture fixture = CreateFixture(organization);

        await fixture.Service.ReactivateAsync(organization.Id, CancellationToken.None);

        Assert.True(organization.Status);
        fixture.Organizations.DidNotReceive().Update(Arg.Any<Organization>());
        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
        await fixture.TenantAccess.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await fixture.Permissions.DidNotReceive()
            .InvalidateOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-106, AC-368
    [Fact]
    public async Task ReactivateAsync_Deactivated_ClearsFieldsAndRecords()
    {
        Organization organization = DeactivatedOrganization();
        Fixture fixture = CreateFixture(organization);

        await fixture.Service.ReactivateAsync(organization.Id, CancellationToken.None);

        Assert.True(organization.Status);
        Assert.Null(organization.DeactivatedAt);
        Assert.Null(organization.DeactivatedByAccountId);
        Assert.False(organization.DeactivatedBySystemAdministrator);
        Assert.Null(organization.DeactivationReason);
        fixture.Organizations.Received(1).Update(organization);
        await fixture.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e =>
                e != null &&
                e.Type == AuditEventType.OrganizationReactivated &&
                e.OrganizationId == organization.Id &&
                e.ActorKind == ActorKind.SystemAdministrator &&
                e.ActorAccountId == fixture.CallerId &&
                e.Parameters.Count == 0),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            fixture.AuditTrail.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
            fixture.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            fixture.TenantAccess.InvalidateOrganizationAsync(organization.Id, Arg.Any<CancellationToken>());
            fixture.Permissions.InvalidateOrganizationAsync(organization.Id, Arg.Any<CancellationToken>());
        });
        Assert.Equal(["save", "commit", "invalidate-tenant-access", "invalidate-permissions"], fixture.Timeline);
    }

    // AC-375
    [Fact]
    public async Task ReactivateAsync_ActiveMember_RecordsAccount()
    {
        Organization organization = DeactivatedOrganization();
        Fixture fixture = CreateFixture(organization, callerIsMember: true);

        await fixture.Service.ReactivateAsync(organization.Id, CancellationToken.None);

        await fixture.AuditTrail.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e != null && e.Type == AuditEventType.OrganizationReactivated && e.ActorKind == ActorKind.Account),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateAsync_UnknownOrganization_Throws404()
    {
        Fixture fixture = CreateFixture();
        Guid unknownId = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Organization>(
            unknownId, () => fixture.Service.ReactivateAsync(unknownId, CancellationToken.None));

        await fixture.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await fixture.AuditTrail.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }
}
