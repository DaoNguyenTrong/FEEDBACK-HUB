using System.Linq.Expressions;
using NSubstitute;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Organizations;

public class OrganizationServiceTests
{
    private sealed record Fixture(
        OrganizationService Service,
        IRepository<Organization, Guid> OrganizationRepo,
        IRepository<OrganizationMember, Guid> MemberRepo,
        IRepository<OrganizationMemberRole, Guid> MemberRoleRepo,
        IRepository<Role, Guid> RoleRepo,
        IRepository<RolePermission, Guid> RolePermissionRepo,
        IUnitOfWork UnitOfWork,
        IRoleService RoleService,
        Account Caller);

    private static Fixture CreateFixture(bool emailConfirmed = true, bool accountExists = true)
    {
        Account caller = Account.Create(new AccountParams("Trần An", $"an-{Guid.NewGuid():N}", "an@example.com"));
        if (emailConfirmed)
        {
            caller.ConfirmEmail();
        }

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        IRepository<OrganizationMember, Guid> memberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<OrganizationMemberRole, Guid> memberRoleRepo =
            Substitute.For<IRepository<OrganizationMemberRole, Guid>>();
        IRepository<Role, Guid> roleRepo = Substitute.For<IRepository<Role, Guid>>();
        IRepository<RolePermission, Guid> rolePermissionRepo = Substitute.For<IRepository<RolePermission, Guid>>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<OrganizationMemberRole, Guid>().Returns(memberRoleRepo);
        unitOfWork.Repository<Role, Guid>().Returns(roleRepo);
        unitOfWork.Repository<RolePermission, Guid>().Returns(rolePermissionRepo);
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);
        accountRepo.GetByIdAsync(caller.Id, Arg.Any<CancellationToken>()).Returns(accountExists ? caller : null);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(caller.Id.ToString());

        IRoleService roleService = Substitute.For<IRoleService>();

        OrganizationService service = new(unitOfWork, currentUserService, roleService);

        return new Fixture(
            service, organizationRepo, memberRepo, memberRoleRepo, roleRepo, rolePermissionRepo, unitOfWork,
            roleService, caller);
    }

    private static Role StubOwnerRoleSeeding(Fixture f)
    {
        Role ownerRole = Role.Create(new RoleParams(Guid.NewGuid(), "Owner", SystemRoleKind.Owner));
        f.RoleService.SeedSystemRolesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<SystemRoleKind, Role> { [SystemRoleKind.Owner] = ownerRole });
        return ownerRole;
    }

    private static async Task AssertNothingAddedAsync(Fixture f)
    {
        await f.OrganizationRepo.DidNotReceive().AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await f.MemberRepo.DidNotReceive().AddAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
        await f.RoleService.DidNotReceive().SeedSystemRolesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrganizationAndAddsCallerAsOwner()
    {
        Fixture f = CreateFixture();
        RepositoryPredicateStub.StubFirstOrDefault(f.OrganizationRepo, []);
        Role ownerRole = StubOwnerRoleSeeding(f);

        OrganizationDto result = await f.Service.CreateAsync(
            new CreateOrganizationRequest("Acme Inc", "acme"), CancellationToken.None);

        Assert.Equal("Acme Inc", result.Name);
        Assert.Equal("acme", result.Slug);
        Assert.Equal(["Owner"], result.MyRoleNames);
        Assert.Equal([new RoleRefDto(ownerRole.Id, "Owner", SystemRoleKind.Owner)], result.MyRoles);
        Assert.Equal(Permissions.All, result.MyPermissionCodes);
        await f.OrganizationRepo.Received(1).AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await f.MemberRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMember>(m => m != null && m.AccountId == f.Caller.Id), Arg.Any<CancellationToken>());
        await f.MemberRoleRepo.Received(1).AddAsync(
            Arg.Is<OrganizationMemberRole>(mr => mr != null && mr.RoleId == ownerRole.Id), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // AC-351, BR-23
    [Fact]
    public async Task CreateAsync_UnconfirmedEmail_Throws403AndAddsNothing()
    {
        Fixture f = CreateFixture(emailConfirmed: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.EmailNotConfirmed,
            () => f.Service.CreateAsync(new CreateOrganizationRequest("Acme Inc", "acme"), CancellationToken.None));

        await f.OrganizationRepo.DidNotReceive()
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<Organization, bool>>>(), Arg.Any<CancellationToken>());
        await AssertNothingAddedAsync(f);
    }

    // A token whose account is gone authenticates nobody: the same 401 as a token without a usable subject.
    [Fact]
    public async Task CreateAsync_AccountMissing_Throws401AndAddsNothing()
    {
        Fixture f = CreateFixture(accountExists: false);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.CreateAsync(new CreateOrganizationRequest("Acme Inc", "acme"), CancellationToken.None));

        await AssertNothingAddedAsync(f);
    }

    // AC-283, AC-284: the slug is checked before anyone looks for a duplicate.
    [Theory]
    [InlineData("ab")]
    [InlineData("acme inc")]
    [InlineData("-acme")]
    public async Task CreateAsync_InvalidSlug_Throws400(string slug)
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            DomainMessages.OrganizationSlugInvalid,
            () => f.Service.CreateAsync(new CreateOrganizationRequest("Acme Inc", slug), CancellationToken.None));

        await f.OrganizationRepo.DidNotReceive()
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<Organization, bool>>>(), Arg.Any<CancellationToken>());
        await AssertNothingAddedAsync(f);
    }

    [Fact]
    public async Task CreateAsync_ExistingSlug_Throws409()
    {
        Fixture f = CreateFixture();
        Organization existing = Organization.Create(new OrganizationParams("Existing", "acme"));
        RepositoryPredicateStub.StubFirstOrDefault(f.OrganizationRepo, [existing]);

        await ApplicationAssert.ThrowsWithMessageAsync<ConflictException>(
            ApplicationMessages.OrganizationSlugAlreadyExists,
            () => f.Service.CreateAsync(new CreateOrganizationRequest("Acme Inc", "  ACME "), CancellationToken.None));

        await AssertNothingAddedAsync(f);
    }

    // ListMineAsync

    // AC-73: a deactivated organization stays in the picker, marked by its status.
    [Fact]
    public async Task ListMineAsync_IncludesDeactivatedWithStatusFalseAndMyRoles()
    {
        Fixture f = CreateFixture();
        Organization active = Organization.Create(new OrganizationParams("An Bình", "an-binh"));
        Organization deactivated = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));
        deactivated.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        OrganizationMember activeMembership =
            OrganizationMember.Create(new OrganizationMemberParams(active.Id, f.Caller.Id));
        OrganizationMember deactivatedMembership =
            OrganizationMember.Create(new OrganizationMemberParams(deactivated.Id, f.Caller.Id));
        Role owner = Role.Create(new RoleParams(active.Id, "Owner", SystemRoleKind.Owner));
        Role reviewer = Role.Create(new RoleParams(deactivated.Id, "Reviewer"));
        RepositoryPredicateStub.StubListAsync(f.MemberRepo, [activeMembership, deactivatedMembership]);
        RepositoryPredicateStub.StubListAsync(f.OrganizationRepo, [active, deactivated]);
        RepositoryPredicateStub.StubListAsync(f.MemberRoleRepo,
        [
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(activeMembership.Id, owner.Id)),
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(deactivatedMembership.Id, reviewer.Id))
        ]);
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [owner, reviewer]);
        RepositoryPredicateStub.StubListAsync(f.RolePermissionRepo,
            [RolePermission.Create(new RolePermissionParams(reviewer.Id, Permissions.AuditLogsView))]);

        IReadOnlyList<OrganizationDto> result = await f.Service.ListMineAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        OrganizationDto first = result.Single(o => o.Id == active.Id);
        Assert.True(first.Status);
        Assert.Equal([new RoleRefDto(owner.Id, "Owner", SystemRoleKind.Owner)], first.MyRoles);
        Assert.Equal(Permissions.All, first.MyPermissionCodes);
        OrganizationDto second = result.Single(o => o.Id == deactivated.Id);
        Assert.False(second.Status);
        Assert.Equal([new RoleRefDto(reviewer.Id, "Reviewer", null)], second.MyRoles);
        Assert.Equal(["Reviewer"], second.MyRoleNames);
        Assert.Equal([Permissions.AuditLogsView], second.MyPermissionCodes);
        await f.OrganizationRepo.Received(1)
            .ListAsync(Arg.Any<Expression<Func<Organization, bool>>>(), Arg.Any<CancellationToken>());
        await f.OrganizationRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // No foreign key ties a member-role link to the member's organization, so a crossing link grants nothing.
    [Fact]
    public async Task ListMineAsync_RoleLinkCrossingOrganizations_IsIgnored()
    {
        Fixture f = CreateFixture();
        Organization mine = Organization.Create(new OrganizationParams("An Bình", "an-binh"));
        Organization other = Organization.Create(new OrganizationParams("Bảo Minh", "bao-minh"));
        OrganizationMember membership = OrganizationMember.Create(new OrganizationMemberParams(mine.Id, f.Caller.Id));
        Role member = Role.Create(new RoleParams(mine.Id, "Member", SystemRoleKind.Member));
        Role foreignOwner = Role.Create(new RoleParams(other.Id, "Owner", SystemRoleKind.Owner));
        RepositoryPredicateStub.StubListAsync(f.MemberRepo, [membership]);
        RepositoryPredicateStub.StubListAsync(f.OrganizationRepo, [mine, other]);
        RepositoryPredicateStub.StubListAsync(f.MemberRoleRepo,
        [
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(membership.Id, member.Id)),
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(membership.Id, foreignOwner.Id))
        ]);
        RepositoryPredicateStub.StubListAsync(f.RoleRepo, [member, foreignOwner]);
        RepositoryPredicateStub.StubListAsync(f.RolePermissionRepo, []);

        IReadOnlyList<OrganizationDto> result = await f.Service.ListMineAsync(CancellationToken.None);

        OrganizationDto only = Assert.Single(result);
        Assert.Equal([new RoleRefDto(member.Id, "Member", SystemRoleKind.Member)], only.MyRoles);
        Assert.Empty(only.MyPermissionCodes);
    }
}
