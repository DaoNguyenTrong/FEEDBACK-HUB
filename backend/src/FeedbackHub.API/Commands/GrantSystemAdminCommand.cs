using FeedbackHub.Application.Services.SystemAdministrators;

namespace FeedbackHub.API.Commands;

/// <summary>Makes the account with a verified <c>--email</c> a system administrator. Idempotent.</summary>
public sealed class GrantSystemAdminCommand(TextWriter? output = null, TextWriter? error = null)
    : SystemAdminCommand(output, error)
{
    public override string Verb => "grant-system-admin";

    protected override Task<SystemAdministratorChange> ApplyAsync(
        ISystemAdministratorService service, string email, CancellationToken cancellationToken) =>
        service.GrantAsync(email, cancellationToken);
}
