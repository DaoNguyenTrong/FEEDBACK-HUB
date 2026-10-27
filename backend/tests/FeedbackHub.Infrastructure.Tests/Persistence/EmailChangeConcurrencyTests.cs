using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

// Commits rows on purpose: the unique index only fires across two real transactions.
[Collection(nameof(PostgresCollection))]
public sealed class EmailChangeConcurrencyTests(PostgresContainerFixture fixture)
{
    private static UnitOfWork CreateUnitOfWork(AppDbContext context) =>
        new(context, new ServiceCollection().BuildServiceProvider());

    private static async Task<Guid> SeedAccountWithPendingAsync(AppDbContext context, string tag, string pending)
    {
        Account account = Account.Create(new AccountParams($"Account {tag}", $"user-{tag}", $"{tag}@example.com"));
        account.RequestEmailChange(pending);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    [Fact]
    public async Task ConfirmEmailChange_TwoAccountsSameAddressConcurrently_OneSucceedsOtherConflict()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        string contested = $"contested-{tag}@example.com";
        Guid firstId;
        Guid secondId;
        await using (AppDbContext seed = fixture.CreateDbContext())
        {
            firstId = await SeedAccountWithPendingAsync(seed, $"a{tag}", contested);
            secondId = await SeedAccountWithPendingAsync(seed, $"b{tag}", contested);
        }

        // Both load before either saves, so the service's duplicate check would pass for both.
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        Account first = await firstContext.Accounts.SingleAsync(a => a.Id == firstId);
        Account second = await secondContext.Accounts.SingleAsync(a => a.Id == secondId);
        first.ApplyEmailChange();
        second.ApplyEmailChange();

        Task firstSave = CreateUnitOfWork(firstContext).SaveChangesAsync();
        Task secondSave = CreateUnitOfWork(secondContext).SaveChangesAsync();
        Task[] saves = [firstSave, secondSave];
        try
        {
            await Task.WhenAll(saves);
        }
        catch (ConflictException)
        {
            // Inspected below, per task.
        }

        List<Task> failed = saves.Where(t => t.IsFaulted).ToList();
        Task failedSave = Assert.Single(failed);
        ConflictException conflict = Assert.IsType<ConflictException>(failedSave.Exception!.InnerException);
        Assert.Equal(ApplicationMessages.AccountEmailAlreadyExists, conflict.Message);

        await using AppDbContext reader = fixture.CreateDbContext();
        Assert.Equal(1, await reader.Accounts.CountAsync(a => a.Email == contested));
    }
}
