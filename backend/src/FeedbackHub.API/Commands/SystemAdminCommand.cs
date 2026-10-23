using FeedbackHub.Application.Services.SystemAdministrators;

namespace FeedbackHub.API.Commands;

/// <summary>
/// Shared shape of <c>grant-system-admin</c> and <c>revoke-system-admin</c>: read <c>--email &lt;email&gt;</c>,
/// call the service, print one line. Exit codes: 0 done (or nothing to do), 1 bad usage, 2 no such account,
/// 3 email not verified. Output carries the username and account id, never the email.
/// </summary>
public abstract class SystemAdminCommand : ICliCommand
{
    public abstract string Verb { get; }

    public string Usage => "--email <email>";

    protected abstract Task<SystemAdministratorChange> ApplyAsync(
        ISystemAdministratorService service, string email, CancellationToken cancellationToken);

    public async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 2 || args[0] != "--email" || string.IsNullOrWhiteSpace(args[1]))
        {
            return 1;
        }

        SystemAdministratorChange change = await ApplyAsync(
            services.GetRequiredService<ISystemAdministratorService>(), args[1], cancellationToken);
        string who = $"{change.Username} ({change.AccountId})";

        switch (change.Outcome)
        {
            case SystemAdministratorChangeOutcome.Granted:
                Console.WriteLine($"Granted system administrator to {who}.");
                return 0;
            case SystemAdministratorChangeOutcome.AlreadyGranted:
                Console.WriteLine($"{who} is already a system administrator.");
                return 0;
            case SystemAdministratorChangeOutcome.Revoked:
                Console.WriteLine($"Revoked system administrator from {who}.");
                return 0;
            case SystemAdministratorChangeOutcome.NotGranted:
                Console.WriteLine($"{who} is not a system administrator.");
                return 0;
            case SystemAdministratorChangeOutcome.AccountNotFound:
                Console.Error.WriteLine("No account has this email.");
                return 2;
            case SystemAdministratorChangeOutcome.EmailNotConfirmed:
                Console.Error.WriteLine(
                    "This account's email is not verified. Ask the person to verify it, then run the command again.");
                return 3;
            default:
                throw new InvalidOperationException($"Unhandled outcome {change.Outcome}.");
        }
    }
}
