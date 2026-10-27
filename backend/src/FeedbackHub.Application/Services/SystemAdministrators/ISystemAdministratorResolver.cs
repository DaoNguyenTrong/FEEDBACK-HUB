namespace FeedbackHub.Application.Services.SystemAdministrators;

/// <summary>Tells whether an account is a system administrator, through a short-TTL cache shared by every instance.</summary>
public interface ISystemAdministratorResolver
{
    Task<bool> IsSystemAdministratorAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Drops the cached answer for every instance at once, so a grant or revoke applies to the next request.</summary>
    Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken);
}
