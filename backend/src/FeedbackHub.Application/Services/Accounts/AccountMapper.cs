using Riok.Mapperly.Abstractions;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Accounts;

[Mapper]
public static partial class AccountMapper
{
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.FailedLoginCount))]
    [MapperIgnoreSource(nameof(Account.FailedLoginWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.LockedUntil))]
    [MapperIgnoreSource(nameof(Account.FailedPasswordChangeCount))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeLockedUntil))]
    [MapperIgnoreSource(nameof(Account.PasswordHash))]
    [MapperIgnoreSource(nameof(Account.AvatarPath))]
    [MapperIgnoreSource(nameof(Account.PendingEmail))]
    [MapperIgnoreSource(nameof(Account.FailedEmailChangeCount))]
    [MapperIgnoreSource(nameof(Account.EmailChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.EmailChangeLockedUntil))]
    [MapperIgnoreTarget(nameof(AccountDto.AvatarUrl))]
    public static partial AccountDto ToDto(Account account);

    [MapperIgnoreSource(nameof(Account.CreatedAt))]
    [MapperIgnoreSource(nameof(Account.UpdatedAt))]
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.FailedLoginCount))]
    [MapperIgnoreSource(nameof(Account.FailedLoginWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.LockedUntil))]
    [MapperIgnoreSource(nameof(Account.FailedPasswordChangeCount))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeLockedUntil))]
    [MapperIgnoreSource(nameof(Account.Status))]
    [MapperIgnoreSource(nameof(Account.AvatarPath))]
    [MapperIgnoreSource(nameof(Account.FailedEmailChangeCount))]
    [MapperIgnoreSource(nameof(Account.EmailChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.EmailChangeLockedUntil))]
    [MapperIgnoreTarget(nameof(ProfileDto.AvatarUrl))]
    [MapProperty(nameof(Account.PasswordHash), nameof(ProfileDto.HasPassword))]
    public static partial ProfileDto ToProfileDto(Account account);

    private static bool MapPasswordHashToHasPassword(string? passwordHash) =>
        !string.IsNullOrWhiteSpace(passwordHash);
}
