using FeedbackHub.Application.Services.SystemAdministrators;

namespace FeedbackHub.API.Commands;

/// <summary>Removes system administration from the account with <c>--email</c>. Idempotent.</summary>
public sealed class RevokeSystemAdminCommand(TextWriter? output = null, TextWriter? error = null)
    : SystemAdminCommand(output, error)
{
    public override string Verb => "revoke-system-admin";

    protected override Task<SystemAdministratorChange> ApplyAsync(
        ISystemAdministratorService service, string email, CancellationToken cancellationToken) =>
        service.RevokeAsync(email, cancellationToken);
}
