namespace FeedbackHub.Application.Services.Accounts;

public interface IAccountLock
{
    /// <summary>
    /// Locks the account's row until the current transaction ends; a concurrent caller waits here, then reads
    /// what the first one committed. Does nothing when no such row exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    Task AcquireAsync(Guid accountId, CancellationToken cancellationToken);
}
