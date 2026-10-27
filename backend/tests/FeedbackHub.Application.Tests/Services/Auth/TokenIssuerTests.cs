using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Application.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Tests.Services.Auth;

public class TokenIssuerTests
{
    private sealed record Fixture(
        TokenIssuer Issuer,
        IUnitOfWork UnitOfWork,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IRepository<OrganizationMember, Guid> OrganizationMemberRepo,
        IRepository<Organization, Guid> OrganizationRepo,
        ISystemAdministratorResolver SystemAdministratorResolver);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        IRepository<OrganizationMember, Guid> organizationMemberRepo =
            Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(organizationMemberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);
        ISystemAdministratorResolver systemAdministratorResolver = Substitute.For<ISystemAdministratorResolver>();

        TokenIssuer issuer = new(
            unitOfWork,
            Substitute.For<IJwtTokenService>(),
            Substitute.For<IPermissionResolver>(),
            systemAdministratorResolver,
            Options.Create(new JwtSettings()),
            Options.Create(new StorageSettings()));

        return new Fixture(
            issuer, unitOfWork, refreshTokenRepo, organizationMemberRepo, organizationRepo, systemAdministratorResolver);
    }

    private static Organization CreateOrganization(string slug, bool suspended = false)
    {
        Organization organization = Organization.Create(new OrganizationParams(slug, slug));
        if (suspended)
        {
            organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        }

        return organization;
    }

    private static void SeedMemberships(Fixture f, Guid accountId, params Organization[] organizations)
    {
        RepositoryPredicateStub.StubListAsync(
            f.OrganizationMemberRepo,
            organizations
                .Select(o => OrganizationMember.Create(new OrganizationMemberParams(o.Id, accountId)))
                .ToList());
        RepositoryPredicateStub.StubListAsync(f.OrganizationRepo, organizations);
    }

    // IssueTokensAsync

    [Fact]
    public async Task IssueTokensAsync_LockedAccount_Throws401AndCreatesNoRefreshToken()
    {
        Fixture f = CreateFixture();
        Account account = Account.Create(new AccountParams("Locked", "locked-user", "locked@example.com"));
        account.Deactivate();

        UnauthorizedException exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            f.Issuer.IssueTokensAsync(
                account, null, null, null, false, DateTime.UtcNow, familyId: null, CancellationToken.None));

        Assert.Equal(ApplicationMessages.InvalidUsernameOrPassword, exception.Message);
        await f.RefreshTokenRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await f.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await f.SystemAdministratorResolver.DidNotReceiveWithAnyArgs().IsSystemAdministratorAsync(default, default);
    }

    // API-27: every sign-in path goes through here, so the flag is right on all six of them.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IssueTokensAsync_SystemAdministratorOrNot_SetsFlagFromResolver(bool isSystemAdministrator)
    {
        Fixture f = CreateFixture();
        Account account = Account.Create(new AccountParams("Operator", "operator-user", "operator@example.com"));
        f.SystemAdministratorResolver.IsSystemAdministratorAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(isSystemAdministrator);

        AuthResult result = await f.Issuer.IssueTokensAsync(
            account, null, null, null, false, DateTime.UtcNow, familyId: null, CancellationToken.None);

        Assert.Equal(isSystemAdministrator, result.IsSystemAdministrator);
        await f.SystemAdministratorResolver.Received(1)
            .IsSystemAdministratorAsync(account.Id, Arg.Any<CancellationToken>());
        await f.RefreshTokenRepo.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // ResolveDefaultOrganizationIdAsync

    [Fact]
    public async Task ResolveDefaultOrganizationIdAsync_OneActiveOneSuspended_ReturnsTheActive()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        Organization active = CreateOrganization("active-co");
        Organization suspended = CreateOrganization("suspended-co", suspended: true);
        SeedMemberships(f, accountId, active, suspended);

        Guid? organizationId = await f.Issuer.ResolveDefaultOrganizationIdAsync(accountId, CancellationToken.None);

        Assert.Equal(active.Id, organizationId);
    }

    // AC-184: an account whose every organization is suspended lands on the welcome page, not in one of them.
    [Fact]
    public async Task ResolveDefaultOrganizationIdAsync_OnlySuspended_ReturnsNull()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        SeedMemberships(f, accountId, CreateOrganization("suspended-co", suspended: true));

        Guid? organizationId = await f.Issuer.ResolveDefaultOrganizationIdAsync(accountId, CancellationToken.None);

        Assert.Null(organizationId);
    }

    [Fact]
    public async Task ResolveDefaultOrganizationIdAsync_TwoActive_ReturnsNull()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        SeedMemberships(f, accountId, CreateOrganization("first-co"), CreateOrganization("second-co"));

        Guid? organizationId = await f.Issuer.ResolveDefaultOrganizationIdAsync(accountId, CancellationToken.None);

        Assert.Null(organizationId);
    }

    [Fact]
    public async Task ResolveDefaultOrganizationIdAsync_NoMembership_ReturnsNullWithoutReadingOrganizations()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        SeedMemberships(f, accountId);

        Guid? organizationId = await f.Issuer.ResolveDefaultOrganizationIdAsync(accountId, CancellationToken.None);

        Assert.Null(organizationId);
        await f.OrganizationRepo.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
    }
}
