using Microsoft.EntityFrameworkCore;
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
/// Real Postgres for the <c>system_administrators</c> rows, real Redis for the cache: each "instance" is its own
/// <see cref="RedisCacheService"/> over the same Redis, as two API replicas would be.
/// </summary>
[Collection(nameof(PostgresCollectionB))]
public sealed class SystemAdministratorResolverTests(PostgresContainerFixture postgres, RedisContainerFixture redis)
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

    private SystemAdministratorResolver CreateResolver(ICacheService cache) =>
        new(new UnitOfWork(context, repositories), cache);

    private async Task<Account> SeedAccountAsync()
    {
        Account account = Account.Create(
            new AccountParams("Operator", $"operator-{Guid.NewGuid():N}", $"operator-{Guid.NewGuid():N}@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return account;
    }

    private async Task<SystemAdministrator> SeedSystemAdministratorAsync(Guid accountId)
    {
        SystemAdministrator administrator = SystemAdministrator.Create(new SystemAdministratorParams(accountId));
        context.SystemAdministrators.Add(administrator);
        await context.SaveChangesAsync();

        return administrator;
    }

    // IsSystemAdministratorAsync

    [Fact]
    public async Task IsSystemAdministratorAsync_RowExists_True()
    {
        Account account = await SeedAccountAsync();
        await SeedSystemAdministratorAsync(account.Id);

        bool result = await CreateResolver(CreateCache()).IsSystemAdministratorAsync(account.Id, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsSystemAdministratorAsync_NoRow_False()
    {
        Account account = await SeedAccountAsync();

        bool result = await CreateResolver(CreateCache()).IsSystemAdministratorAsync(account.Id, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsSystemAdministratorAsync_Always_CachesUnderSystemAdminScopeByAccountForSixtySeconds()
    {
        Guid accountId = Guid.NewGuid();
        ICacheService cache = Substitute.For<ICacheService>();
        cache.GetOrSetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        bool result = await CreateResolver(cache).IsSystemAdministratorAsync(accountId, CancellationToken.None);

        Assert.True(result);
        await cache.Received(1).GetOrSetAsync(
            "system-admin", accountId.ToString(), Arg.Any<Func<CancellationToken, Task<bool>>>(),
            TimeSpan.FromSeconds(60), Arg.Any<CancellationToken>());
    }

    // InvalidateAsync

    // Revoking on one instance must deny the next request on every other one, not after the TTL (QĐ-15).
    [Fact]
    public async Task InvalidateAsync_OnOneInstance_OtherInstanceSeesRevocationAtOnce()
    {
        Account account = await SeedAccountAsync();
        SystemAdministrator administrator = await SeedSystemAdministratorAsync(account.Id);
        SystemAdministratorResolver firstInstance = CreateResolver(CreateCache());
        SystemAdministratorResolver secondInstance = CreateResolver(CreateCache());

        Assert.True(await firstInstance.IsSystemAdministratorAsync(account.Id, CancellationToken.None));
        await context.SystemAdministrators.Where(a => a.Id == administrator.Id).ExecuteDeleteAsync();
        Assert.True(await firstInstance.IsSystemAdministratorAsync(account.Id, CancellationToken.None));

        await secondInstance.InvalidateAsync(account.Id, CancellationToken.None);

        Assert.False(await firstInstance.IsSystemAdministratorAsync(account.Id, CancellationToken.None));
    }

    // Persistence (M2)

    // No organization can see the snapshot: it carries no organization (TECH00 §3.5).
    [Fact]
    public async Task SaveChanges_SystemAdministratorSnapshot_HasNoOrganization()
    {
        Account account = await SeedAccountAsync();

        SystemAdministrator administrator = await SeedSystemAdministratorAsync(account.Id);

        AuditLog snapshot = await context.AuditLogs.SingleAsync(log =>
            log.EntityName == nameof(SystemAdministrator) && log.EntityId == administrator.Id.ToString());
        Assert.Null(snapshot.OrganizationId);
        Assert.Equal(AuditLogKind.Change, snapshot.Kind);
        Assert.Equal("Added", snapshot.Action);
    }

    [Fact]
    public async Task DeleteAccount_CascadesSystemAdministrator()
    {
        Account account = await SeedAccountAsync();
        await SeedSystemAdministratorAsync(account.Id);

        await context.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync();

        Assert.False(await context.SystemAdministrators.AnyAsync(a => a.AccountId == account.Id));
    }

    [Fact]
    public async Task SameAccountTwice_IsRejectedByTheDatabase()
    {
        Account account = await SeedAccountAsync();
        await SeedSystemAdministratorAsync(account.Id);

        await Assert.ThrowsAsync<DbUpdateException>(() => SeedSystemAdministratorAsync(account.Id));
    }

    [Fact]
    public async Task UnknownAccount_IsRejectedByTheDatabase()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => SeedSystemAdministratorAsync(Guid.NewGuid()));
    }
}
