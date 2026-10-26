using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Auth;

internal static class AccountUniqueness
{
    // Username first, then email, for both password registrations (plain and through an invitation). A race past
    // the email read still answers 409 through the unique index on Email (mapped in UnitOfWork.SaveChangesAsync).
    internal static async Task EnsureAvailableAsync(
        IRepository<Account, Guid> accounts, string username, string email, CancellationToken cancellationToken)
    {
        string normalizedUsername = Account.NormalizeUsername(username);
        if (await accounts.FirstOrDefaultAsync(
                account => account.Username == normalizedUsername, cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountUsernameAlreadyExists);
        }

        string normalizedEmail = Account.NormalizeEmail(email);
        if (await accounts.FirstOrDefaultAsync(
                account => account.Email == normalizedEmail, cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }
    }
}
