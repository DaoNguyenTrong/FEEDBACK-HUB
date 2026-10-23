using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.SystemAdministrators;

public sealed class SystemAdministratorService(
    IUnitOfWork unitOfWork,
    ISystemAdministratorResolver resolver) : ISystemAdministratorService
{
    public async Task<SystemAdministratorChange> GrantAsync(string email, CancellationToken cancellationToken)
    {
        Account? account = await FindAccountAsync(email, cancellationToken);
        if (account is null)
        {
            return new SystemAdministratorChange(SystemAdministratorChangeOutcome.AccountNotFound, null, null);
        }

        // Only a verified email proves the address belongs to the person being promoted.
        if (!account.EmailConfirmed)
        {
            return Result(SystemAdministratorChangeOutcome.EmailNotConfirmed, account);
        }

        IRepository<SystemAdministrator, Guid> administrators = unitOfWork.Repository<SystemAdministrator, Guid>();
        if (await administrators.FirstOrDefaultAsync(row => row.AccountId == account.Id, cancellationToken) is not null)
        {
            return Result(SystemAdministratorChangeOutcome.AlreadyGranted, account);
        }

        await administrators.AddAsync(SystemAdministrator.Create(new SystemAdministratorParams(account.Id)), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // A cached "false" would otherwise keep the new administrator out for up to its TTL.
        await resolver.InvalidateAsync(account.Id, cancellationToken);
        return Result(SystemAdministratorChangeOutcome.Granted, account);
    }

    public async Task<SystemAdministratorChange> RevokeAsync(string email, CancellationToken cancellationToken)
    {
        Account? account = await FindAccountAsync(email, cancellationToken);
        if (account is null)
        {
            return new SystemAdministratorChange(SystemAdministratorChangeOutcome.AccountNotFound, null, null);
        }

        IRepository<SystemAdministrator, Guid> administrators = unitOfWork.Repository<SystemAdministrator, Guid>();
        SystemAdministrator? row = await administrators.FirstOrDefaultAsync(r => r.AccountId == account.Id, cancellationToken);
        if (row is null)
        {
            return Result(SystemAdministratorChangeOutcome.NotGranted, account);
        }

        administrators.Delete(row);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await resolver.InvalidateAsync(account.Id, cancellationToken);
        return Result(SystemAdministratorChangeOutcome.Revoked, account);
    }

    // Matches Email only: a pending email is unverified and may belong to someone else.
    private Task<Account?> FindAccountAsync(string email, CancellationToken cancellationToken)
    {
        string normalized = Account.NormalizeEmail(email);
        return unitOfWork.Repository<Account, Guid>().FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken);
    }

    private static SystemAdministratorChange Result(SystemAdministratorChangeOutcome outcome, Account account) =>
        new(outcome, account.Id, account.Username);
}
