using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose: a row lock only shows between two real transactions.
[Collection(nameof(PostgresCollection))]
public sealed class AccountLockTests(PostgresContainerFixture fixture)
{
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan NotBlockedLimit = TimeSpan.FromSeconds(10);

    private async Task<Account> SeedAccountAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        string tag = Guid.NewGuid().ToString("N");
        Account account = Account.Create(new AccountParams("Lock", $"lock-{tag}", $"lock-{tag}@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task AcquireAsync_TwoTransactionsSameAccount_RunOneAfterTheOther()
    {
        Account seeded = await SeedAccountAsync();
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using IDbContextTransaction first = await firstContext.Database.BeginTransactionAsync();
        await using IDbContextTransaction second = await secondContext.Database.BeginTransactionAsync();
        await new AccountLock(firstContext).AcquireAsync(seeded.Id, CancellationToken.None);

        Task waiting = new AccountLock(secondContext).AcquireAsync(seeded.Id, CancellationToken.None);
        await Task.Delay(BlockedWindow);
        Assert.False(waiting.IsCompleted);

        await first.CommitAsync();
        await waiting;
    }

    [Fact]
    public async Task AcquireAsync_DifferentAccounts_DoNotBlock()
    {
        Account firstAccount = await SeedAccountAsync();
        Account secondAccount = await SeedAccountAsync();
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        await using IDbContextTransaction first = await firstContext.Database.BeginTransactionAsync();
        await using IDbContextTransaction second = await secondContext.Database.BeginTransactionAsync();
        await new AccountLock(firstContext).AcquireAsync(firstAccount.Id, CancellationToken.None);

        Task other = new AccountLock(secondContext).AcquireAsync(secondAccount.Id, CancellationToken.None);
        Task finished = await Task.WhenAny(other, Task.Delay(NotBlockedLimit));

        Assert.Same(other, finished);
        await other;
    }

    [Fact]
    public async Task AcquireAsync_NoTransaction_Throws()
    {
        Account seeded = await SeedAccountAsync();
        await using AppDbContext context = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AccountLock(context).AcquireAsync(seeded.Id, CancellationToken.None));
    }
}
