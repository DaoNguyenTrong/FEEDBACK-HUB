using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Migrations;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Tests.TestSupport;

namespace FeedbackHub.Infrastructure.Tests.Persistence.Migrations;

// docs/tech/00-nen-tang.md §10.1 M10.
[Collection(nameof(PostgresCollectionB))]
public sealed class AddAccountPendingEmailStageTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
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

    // Seeds the state an old machine leaves behind: PendingEmail set, stage NULL.
    private async Task<Account> SeedAsync(string tag, bool password, bool confirmed, bool pending)
    {
        Account account = Account.Create(new AccountParams($"Name {tag}", $"user-{tag}", $"{tag}@example.com"));
        if (confirmed)
        {
            account.ConfirmEmail();
        }

        if (password)
        {
            account.SetPasswordHash("hash-value");
        }

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        if (pending)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE accounts SET "PendingEmail" = {tag + "-new@example.com"} WHERE "Id" = {account.Id}""");
            context.Set<EmailVerificationToken>().Add(EmailVerificationToken.Create(new EmailVerificationTokenParams(
                account.Id, $"hash-{tag}", DateTime.UtcNow.AddHours(1), EmailVerificationPurpose.ChangeEmail,
                $"{tag}-new@example.com")));
            await context.SaveChangesAsync();
        }

        return account;
    }

    private async Task<(string? Pending, string? Stage)> RowAsync(Account account)
    {
        List<string?> values = await context.Database
            .SqlQuery<string>($"""SELECT coalesce("PendingEmail", '<null>') || '|' || coalesce("PendingEmailStage", '<null>') AS "Value" FROM accounts WHERE "Id" = {account.Id}""")
            .ToListAsync();
        string[] parts = values.Single()!.Split('|');
        return (parts[0] == "<null>" ? null : parts[0], parts[1] == "<null>" ? null : parts[1]);
    }

    private async Task<int> UnconsumedTokensAsync(Account account) =>
        await context.Set<EmailVerificationToken>().AsNoTracking()
            .CountAsync(t => t.AccountId == account.Id && t.ConsumedAt == null);

    private Task RunAsync() => context.Database.ExecuteSqlRawAsync(AddAccountPendingEmailStage.DataSql);

    [Fact]
    public async Task DataSql_PasswordAccountWithPending_BecomesAwaitingNewEmailAndKeepsTokens()
    {
        Account account = await SeedAsync("pw", password: true, confirmed: true, pending: true);

        await RunAsync();

        (string? pending, string? stage) = await RowAsync(account);
        Assert.Equal("pw-new@example.com", pending);
        Assert.Equal("AwaitingNewEmail", stage);
        Assert.Equal(1, await UnconsumedTokensAsync(account));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DataSql_PasswordlessAccountWithPending_IsClearedAndTokensConsumed(bool confirmed)
    {
        Account account = await SeedAsync("nopw", password: false, confirmed: confirmed, pending: true);

        await RunAsync();

        (string? pending, string? stage) = await RowAsync(account);
        Assert.Null(pending);
        Assert.Null(stage);
        Assert.Equal(0, await UnconsumedTokensAsync(account));
        Assert.Equal(1, await context.Set<EmailVerificationToken>().AsNoTracking()
            .CountAsync(t => t.AccountId == account.Id && t.ConsumedAt != null));
    }

    [Fact]
    public async Task DataSql_AccountsWithoutPending_AreUnchanged()
    {
        Account passwordless = await SeedAsync("idle1", password: false, confirmed: true, pending: false);
        Account withPassword = await SeedAsync("idle2", password: true, confirmed: true, pending: false);

        await RunAsync();

        Assert.Equal((null, null), await RowAsync(passwordless));
        Assert.Equal((null, null), await RowAsync(withPassword));
    }

    [Fact]
    public async Task DataSql_RunTwice_ChangesNothingMore()
    {
        Account password = await SeedAsync("twice1", password: true, confirmed: true, pending: true);
        Account passwordless = await SeedAsync("twice2", password: false, confirmed: true, pending: true);
        await RunAsync();
        (string?, string?) firstPassword = await RowAsync(password);
        (string?, string?) firstPasswordless = await RowAsync(passwordless);
        int firstTokens = await UnconsumedTokensAsync(password);

        await RunAsync();

        Assert.Equal(firstPassword, await RowAsync(password));
        Assert.Equal(firstPasswordless, await RowAsync(passwordless));
        Assert.Equal(firstTokens, await UnconsumedTokensAsync(password));
    }
}
