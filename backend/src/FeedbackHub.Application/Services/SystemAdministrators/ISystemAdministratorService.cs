namespace FeedbackHub.Application.Services.SystemAdministrators;

/// <summary>Grants and revokes system administration. Reached only from the server's CLI commands, never over HTTP.</summary>
public interface ISystemAdministratorService
{
    /// <summary>Grants to the account whose confirmed email is <paramref name="email"/>; an unverified email is refused.</summary>
    Task<SystemAdministratorChange> GrantAsync(string email, CancellationToken cancellationToken);

    Task<SystemAdministratorChange> RevokeAsync(string email, CancellationToken cancellationToken);
}
