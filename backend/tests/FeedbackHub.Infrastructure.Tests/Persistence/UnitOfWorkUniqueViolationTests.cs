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
public sealed class UnitOfWorkUniqueViolationTests(PostgresContainerFixture fixture)
{
    private static UnitOfWork CreateUnitOfWork(AppDbContext context) =>
        new(context, new ServiceCollection().BuildServiceProvider());

    // SaveChangesAsync

    // Both requests pass the service's duplicate check before either saves; the index decides, and the loser
    // gets the same 409 as the check would have given.
    [Fact]
    public async Task SaveChanges_DuplicateSlugFromTwoContexts_SecondThrowsConflict()
    {
        string slug = $"race-{Guid.NewGuid():N}";
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        firstContext.Organizations.Add(Organization.Create(new OrganizationParams("First", slug)));
        secondContext.Organizations.Add(Organization.Create(new OrganizationParams("Second", slug)));

        await CreateUnitOfWork(firstContext).SaveChangesAsync();
        ConflictException conflict = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUnitOfWork(secondContext).SaveChangesAsync());

        Assert.Equal(ApplicationMessages.OrganizationSlugAlreadyExists, conflict.Message);
        await using AppDbContext reader = fixture.CreateDbContext();
        Assert.Equal(1, await reader.Organizations.CountAsync(o => o.Slug == slug));
    }

    // Two registrations with one username, both past the service's username check: the loser gets the check's 409.
    [Fact]
    public async Task SaveChanges_SameUsernameFromTwoContextsConcurrently_OneSucceedsOtherConflict()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        string username = $"race-{tag}";
        await using AppDbContext firstContext = fixture.CreateDbContext();
        await using AppDbContext secondContext = fixture.CreateDbContext();
        firstContext.Accounts.Add(Account.Create(new AccountParams("First", username, $"a{tag}@example.com")));
        secondContext.Accounts.Add(
            Account.Create(new AccountParams("Second", username.ToUpperInvariant(), $"b{tag}@example.com")));

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

        Task failedSave = Assert.Single(saves, save => save.IsFaulted);
        ConflictException conflict = Assert.IsType<ConflictException>(failedSave.Exception!.InnerException);
        Assert.Equal(ApplicationMessages.AccountUsernameAlreadyExists, conflict.Message);

        await using AppDbContext reader = fixture.CreateDbContext();
        Assert.Equal(1, await reader.Accounts.CountAsync(a => a.Username == username));
    }
}
