using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollection))]
public sealed class SystemAdministratorPersistenceTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task SaveChangesAsync_SecondRowForSameAccount_ThrowsConflict()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        await using AppDbContext seed = fixture.CreateDbContext();
        Account account = Account.Create(new AccountParams($"Account {tag}", $"user-{tag}", $"{tag}@example.com"));
        seed.Accounts.Add(account);
        seed.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));
        await seed.SaveChangesAsync();

        await using AppDbContext second = fixture.CreateDbContext();
        second.SystemAdministrators.Add(SystemAdministrator.Create(new SystemAdministratorParams(account.Id)));
        UnitOfWork unitOfWork = new(second, new ServiceCollection().BuildServiceProvider());

        ConflictException conflict = await Assert.ThrowsAsync<ConflictException>(() => unitOfWork.SaveChangesAsync());

        Assert.Equal(ApplicationMessages.SystemAdministratorAlreadyGranted, conflict.Message);
    }
}
