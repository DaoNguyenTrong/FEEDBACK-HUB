using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Repositories;
using FeedbackHub.Infrastructure.Services.Auth;
using FeedbackHub.Infrastructure.Services.Caching;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth;

/// <summary>
/// Real Postgres for the membership and organization rows, real Redis for the cache: each "instance" is its own
/// <see cref="RedisCacheService"/> over the same Redis, as two API replicas would be.
/// </summary>
[Collection(nameof(PostgresCollectionB))]
public sealed class TenantAccessServiceTests(PostgresContainerFixture postgres, RedisContainerFixture redis)
    : IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private ServiceProvider repositories = null!;

    public async Task InitializeAsync()
    {
        context = postgres.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        repositories = new ServiceCollection()
            .AddSingleton(context)
            .AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>))
            .BuildServiceProvider();
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await repositories.DisposeAsync();
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private ICacheService CreateCache() =>
        new RedisCacheService(
            new StackExchangeRedisStringStore(redis.Multiplexer),
            Options.Create(new CacheSettings()),
            NullLogger<RedisCacheService>.Instance);

    private TenantAccessService CreateService(ICacheService cache, IUnitOfWork? unitOfWork = null) =>
        new(
            unitOfWork ?? new UnitOfWork(context, repositories),
            cache,
            Options.Create(new TenantAccessSettings { CacheTtlSeconds = 60 }));

    private TenantAccessService CreateService() => CreateService(CreateCache());

    private async Task<Organization> SeedOrganizationAsync()
    {
        Organization organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        return organization;
    }

    private async Task<Account> SeedAccountAsync()
    {
        Account account = Account.Create(
            new AccountParams("Member", $"member-{Guid.NewGuid():N}", $"member-{Guid.NewGuid():N}@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return account;
    }

    private async Task<OrganizationMember> SeedMemberAsync(Guid organizationId, Guid accountId)
    {
        OrganizationMember member = OrganizationMember.Create(new OrganizationMemberParams(organizationId, accountId));
        context.OrganizationMembers.Add(member);
        await context.SaveChangesAsync();

        return member;
    }

    private async Task SuspendAsync(Organization organization)
    {
        organization.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        context.Organizations.Update(organization);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAccessAsync_ActiveMemberActiveOrganization_ReturnsActive()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        await SeedMemberAsync(organization.Id, account.Id);

        TenantAccessStatus status = await CreateService().GetAccessAsync(account.Id, organization.Id);

        Assert.Equal(TenantAccessStatus.Active, status);
    }

    [Fact]
    public async Task GetAccessAsync_ActiveMemberSuspendedOrganization_ReturnsSuspended()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        await SeedMemberAsync(organization.Id, account.Id);
        await SuspendAsync(organization);

        TenantAccessStatus status = await CreateService().GetAccessAsync(account.Id, organization.Id);

        Assert.Equal(TenantAccessStatus.Suspended, status);
    }

    // AC-320: someone who never joined, or who left, a suspended organization is told nothing about its state.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAccessAsync_NotMemberOfSuspendedOrganization_ReturnsNotMember(bool leftBefore)
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        if (leftBefore)
        {
            OrganizationMember member = await SeedMemberAsync(organization.Id, account.Id);
            member.Deactivate();
            context.OrganizationMembers.Update(member);
            await context.SaveChangesAsync();
        }

        await SuspendAsync(organization);

        TenantAccessStatus status = await CreateService().GetAccessAsync(account.Id, organization.Id);

        Assert.Equal(TenantAccessStatus.NotMember, status);
    }

    [Fact]
    public async Task GetAccessAsync_NotMember_DoesNotReadOrganization()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        IRepository<Organization, Guid> organizationRepository = Substitute.For<IRepository<Organization, Guid>>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(new GenericRepository<OrganizationMember, Guid>(context));
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepository);

        TenantAccessStatus status =
            await CreateService(CreateCache(), unitOfWork).GetAccessAsync(account.Id, organization.Id);

        Assert.Equal(TenantAccessStatus.NotMember, status);
        await organizationRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // AC-190 (cache part): a suspension made through one instance is seen by every other one at its next request.
    [Fact]
    public async Task GetAccessAsync_InvalidatedOnOneInstance_OtherInstanceSeesChangeAtOnce()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        await SeedMemberAsync(organization.Id, account.Id);
        TenantAccessService firstInstance = CreateService(CreateCache());
        TenantAccessService secondInstance = CreateService(CreateCache());

        Assert.Equal(TenantAccessStatus.Active, await firstInstance.GetAccessAsync(account.Id, organization.Id));
        await SuspendAsync(organization);
        Assert.Equal(TenantAccessStatus.Active, await firstInstance.GetAccessAsync(account.Id, organization.Id));

        await secondInstance.InvalidateOrganizationAsync(organization.Id);

        Assert.Equal(TenantAccessStatus.Suspended, await firstInstance.GetAccessAsync(account.Id, organization.Id));
    }

    [Fact]
    public async Task InvalidateMemberAsync_MemberLeft_NextReadReturnsNotMember()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        OrganizationMember member = await SeedMemberAsync(organization.Id, account.Id);
        TenantAccessService service = CreateService();
        Assert.Equal(TenantAccessStatus.Active, await service.GetAccessAsync(account.Id, organization.Id));

        member.Deactivate();
        context.OrganizationMembers.Update(member);
        await context.SaveChangesAsync();
        await service.InvalidateMemberAsync(organization.Id, account.Id);

        Assert.Equal(TenantAccessStatus.NotMember, await service.GetAccessAsync(account.Id, organization.Id));
    }

    // A deployed instance may hold a bool under the bare account id in this scope until its TTL; read as the enum it
    // would fail to deserialize (a 500 on every gated request), so the status must live under a different key.
    [Fact]
    public async Task GetAccessAsync_BoolCachedUnderPreviousKey_IsNotRead()
    {
        Organization organization = await SeedOrganizationAsync();
        Account account = await SeedAccountAsync();
        ICacheService cache = CreateCache();
        await cache.SetAsync($"tenant-access:{organization.Id}", account.Id.ToString(), true, TimeSpan.FromSeconds(60));

        TenantAccessStatus status = await CreateService(cache).GetAccessAsync(account.Id, organization.Id);

        Assert.Equal(TenantAccessStatus.NotMember, status);
    }
}
