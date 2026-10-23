namespace FeedbackHub.Application.Services.SystemAdministrators;

public enum SystemAdministratorChangeOutcome
{
    Granted,
    AlreadyGranted,
    Revoked,
    NotGranted,
    AccountNotFound,
    EmailNotConfirmed
}

/// <summary>What a grant or revoke did. <see cref="AccountId"/> and <see cref="Username"/> are null when no account matched.</summary>
public sealed record SystemAdministratorChange(
    SystemAdministratorChangeOutcome Outcome,
    Guid? AccountId,
    string? Username);
