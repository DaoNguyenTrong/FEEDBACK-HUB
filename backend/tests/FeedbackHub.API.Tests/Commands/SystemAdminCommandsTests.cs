using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.API.Commands;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Commands;

[Collection(nameof(ApiCollection))]
public sealed class SystemAdminCommandsTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<Account> SeedAccountAsync(string email, bool confirmed = true)
    {
        await using AppDbContext context = CreateDbContext();
        string suffix = Guid.NewGuid().ToString("N");
        Account account = Account.Create(new AccountParams("Boss", $"boss-{suffix}", email));
        if (confirmed)
        {
            account.ConfirmEmail();
        }

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    // Console is process-wide; the tests of this collection run one at a time, so redirecting it here is safe.
    private async Task<(int? ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter output = new();
        StringWriter error = new();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            int? exitCode = await CliCommands.TryRunAsync(fixture.Services, args, CancellationToken.None);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private async Task<bool> IsAdministratorAsync(Guid accountId)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.SystemAdministrators.AnyAsync(row => row.AccountId == accountId);
    }

    [Theory]
    [InlineData("grant-system-admin")]
    [InlineData("grant-system-admin", "--email")]
    [InlineData("grant-system-admin", "--email", "a@example.com", "--force")]
    [InlineData("grant-system-admin", "--mail", "a@example.com")]
    [InlineData("revoke-system-admin")]
    [InlineData("revoke-system-admin", "--email")]
    [InlineData("revoke-system-admin", "--email", "a@example.com", "--force")]
    public async Task Command_BadArguments_ExitsOneWithUsage(params string[] args)
    {
        (int? exitCode, _, string error) = await RunAsync(args);

        Assert.Equal(1, exitCode);
        Assert.Contains($"{args[0]} --email <email>", error);
    }

    // AC-51
    [Fact]
    public async Task Grant_ConfirmedEmailWithOtherCaseAndSpaces_ExitsZeroAndPrintsUsernameAndId()
    {
        Account account = await SeedAccountAsync("boss@example.com");

        (int? exitCode, string output, _) = await RunAsync("grant-system-admin", "--email", "  BOSS@Example.com ");

        Assert.Equal(0, exitCode);
        Assert.Contains($"Granted system administrator to {account.Username} ({account.Id}).", output);
        Assert.True(await IsAdministratorAsync(account.Id));
    }

    [Fact]
    public async Task Grant_Twice_SecondRunSaysAlreadyAndExitsZero()
    {
        Account account = await SeedAccountAsync("boss@example.com");
        await RunAsync("grant-system-admin", "--email", "boss@example.com");

        (int? exitCode, string output, _) = await RunAsync("grant-system-admin", "--email", "boss@example.com");

        Assert.Equal(0, exitCode);
        Assert.Contains($"{account.Username} ({account.Id}) is already a system administrator.", output);
    }

    [Fact]
    public async Task Grant_Unverified_ExitsThree()
    {
        Account account = await SeedAccountAsync("boss@example.com", confirmed: false);

        (int? exitCode, _, string error) = await RunAsync("grant-system-admin", "--email", "boss@example.com");

        Assert.Equal(3, exitCode);
        Assert.Contains("This account's email is not verified.", error);
        Assert.False(await IsAdministratorAsync(account.Id));
    }

    [Fact]
    public async Task Grant_Unknown_ExitsTwo()
    {
        (int? exitCode, _, string error) = await RunAsync("grant-system-admin", "--email", "nobody@example.com");

        Assert.Equal(2, exitCode);
        Assert.Contains("No account has this email.", error);
    }

    [Fact]
    public async Task Grant_Output_DoesNotContainEmail()
    {
        await SeedAccountAsync("boss@example.com");

        (_, string output, string error) = await RunAsync("grant-system-admin", "--email", "boss@example.com");

        Assert.DoesNotContain("boss@example.com", output + error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Revoke_NotGranted_SaysSoAndExitsZero()
    {
        Account account = await SeedAccountAsync("boss@example.com");

        (int? exitCode, string output, _) = await RunAsync("revoke-system-admin", "--email", "boss@example.com");

        Assert.Equal(0, exitCode);
        Assert.Contains($"{account.Username} ({account.Id}) is not a system administrator.", output);
    }

    [Fact]
    public async Task Revoke_Unknown_ExitsTwo()
    {
        (int? exitCode, _, _) = await RunAsync("revoke-system-admin", "--email", "nobody@example.com");

        Assert.Equal(2, exitCode);
    }

    // AC-52: the cached answer is dropped, so the very next request is refused.
    [Fact]
    public async Task Revoke_ThenNextRequest_Returns403()
    {
        Account account = await SeedAccountAsync("boss@example.com");
        (int? grantExit, _, _) = await RunAsync("grant-system-admin", "--email", "boss@example.com");
        Assert.Equal(0, grantExit);
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, null));

        HttpResponseMessage before = await client.GetAsync("/api/system/organizations");

        (int? revokeExit, string output, _) = await RunAsync("revoke-system-admin", "--email", "boss@example.com");
        HttpResponseMessage after = await client.GetAsync("/api/system/organizations");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(0, revokeExit);
        Assert.Contains($"Revoked system administrator from {account.Username} ({account.Id}).", output);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Contains(ApplicationMessages.SystemAdministratorRequired, await after.Content.ReadAsStringAsync());
    }

    // A cached "not an administrator" must not outlive the grant.
    [Fact]
    public async Task Grant_AfterRefusedRequest_NextRequestSucceeds()
    {
        Account account = await SeedAccountAsync("boss@example.com");
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, null));
        HttpResponseMessage before = await client.GetAsync("/api/system/organizations");

        await RunAsync("grant-system-admin", "--email", "boss@example.com");
        HttpResponseMessage after = await client.GetAsync("/api/system/organizations");

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }
}
