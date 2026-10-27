using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence;

[Collection(nameof(PostgresCollectionB))]
public sealed class AuditExcludedPropertiesTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private static readonly string[] Sensitive =
    [
        "PasswordHash",
        "FailedLoginCount", "FailedLoginWindowStartedAt", "LockedUntil",
        "FailedPasswordChangeCount", "PasswordChangeWindowStartedAt", "PasswordChangeLockedUntil",
        "FailedEmailChangeCount", "EmailChangeWindowStartedAt", "EmailChangeLockedUntil"
    ];

    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    [Fact]
    public async Task SaveChanges_AccountSnapshot_ExcludesPasswordHashAndLockoutColumns()
    {
        Account account = Account.Create(new AccountParams("Audit", "audit-user", "audit@example.com"));
        account.SetPasswordHash("hash-value");
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        account.RequestEmailChange("new-audit@example.com");
        account.SetPasswordHash("other-hash");
        await context.SaveChangesAsync();

        context.Accounts.Remove(account);
        await context.SaveChangesAsync();

        List<AuditLog> logs = await context.AuditLogs
            .Where(log => log.EntityName == nameof(Account) && log.EntityId == account.Id.ToString())
            .ToListAsync();

        Assert.Equal(3, logs.Count);
        foreach (AuditLog log in logs)
        {
            foreach (string? json in new[] { log.OldValues, log.NewValues })
            {
                foreach (string key in Sensitive)
                {
                    Assert.DoesNotContain($"\"{key}\"", json ?? "");
                }
            }
        }

        AuditLog added = logs.Single(log => log.Action == nameof(EntityState.Added));
        Assert.Contains("\"Email\"", added.NewValues);
        Assert.Contains("\"PendingEmail\"", added.NewValues);
        AuditLog modified = logs.Single(log => log.Action == nameof(EntityState.Modified));
        Assert.Contains("\"PendingEmail\"", modified.NewValues);
        AuditLog deleted = logs.Single(log => log.Action == nameof(EntityState.Deleted));
        Assert.Contains("\"Email\"", deleted.OldValues);
    }
}
