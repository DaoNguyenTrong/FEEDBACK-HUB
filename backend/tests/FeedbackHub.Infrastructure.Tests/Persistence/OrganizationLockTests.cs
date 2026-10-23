using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose: a row lock only shows between two real transactions.
[Collection(nameof(PostgresCollection))]
public sealed class OrganizationLockTests(PostgresContainerFixture fixture)
{
    // Long enough for a lock request that is not blocked to finish, short enough to keep the suite quick.
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromMilliseconds(500);

    private async Task<Organization> SeedOrganizationAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        Organization organization = Organization.Create(
            new OrganizationParams("Bảo Minh", $"lock-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    // AcquireAsync

    [Fact]
    public async Task AcquireAsync_SecondTransactionWaitsUntilFirstCommits()
    {
        Organization seeded = await SeedOrganizationAsync();
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using IDbContextTransaction first = await firstContext.Database.BeginTransactionAsync();
        await using IDbContextTransaction second = await secondContext.Database.BeginTransactionAsync();
        await new OrganizationLock(firstContext).AcquireAsync(seeded.Id, CancellationToken.None);
        Organization changed = await firstContext.Organizations.SingleAsync(o => o.Id == seeded.Id);
        changed.Deactivate(new DeactivationParams(Guid.NewGuid(), false, null, DateTime.UtcNow));
        await firstContext.SaveChangesAsync();

        Task waiting = new OrganizationLock(secondContext).AcquireAsync(seeded.Id, CancellationToken.None);
        await Task.Delay(BlockedWindow);
        Assert.False(waiting.IsCompleted);

        await first.CommitAsync();
        await waiting;

        Organization afterLock = await secondContext.Organizations.AsNoTracking().SingleAsync(o => o.Id == seeded.Id);
        Assert.False(afterLock.Status);
    }

    [Fact]
    public async Task AcquireAsync_DifferentOrganizations_DoNotBlock()
    {
        Organization firstOrganization = await SeedOrganizationAsync();
        Organization secondOrganization = await SeedOrganizationAsync();
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using IDbContextTransaction first = await firstContext.Database.BeginTransactionAsync();
        await using IDbContextTransaction second = await secondContext.Database.BeginTransactionAsync();
        await new OrganizationLock(firstContext).AcquireAsync(firstOrganization.Id, CancellationToken.None);

        Task other = new OrganizationLock(secondContext).AcquireAsync(secondOrganization.Id, CancellationToken.None);
        Task finished = await Task.WhenAny(other, Task.Delay(BlockedWindow));

        Assert.Same(other, finished);
        await other;
    }

    // Outside a transaction the lock would end with the statement and protect nothing.
    [Fact]
    public async Task AcquireAsync_OutsideTransaction_Throws()
    {
        Organization seeded = await SeedOrganizationAsync();
        await using AppDbContext context = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new OrganizationLock(context).AcquireAsync(seeded.Id, CancellationToken.None));
    }
}
