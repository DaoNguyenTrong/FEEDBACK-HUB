using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Accounts;

public sealed class AccountService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IPasswordHasher passwordHasher,
    IPasswordAttemptStore passwordAttemptStore,
    IOptions<LoginLockoutSettings> lockoutOptions,
    IStorageService storageService,
    IOptions<StorageSettings> storageOptions,
    EmailChangeLink emailChangeLink,
    ILogger<AccountService> logger) : IAccountService
{
    public const long MaxAvatarBytes = 2 * 1024 * 1024;

    // Under public/ so the S3 providers' anonymous-read policy covers avatars and nothing else.
    private const string AvatarFolder = "public/avatars";

    private readonly StorageSettings storageSettings = storageOptions.Value;
    private readonly LoginLockoutSettings lockoutSettings = lockoutOptions.Value;

    public async Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        Account account = await GetCurrentAccountAsync(cancellationToken);

        return ToProfile(account);
    }

    public async Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        string email = Account.NormalizeEmail(request.Email);
        bool emailChanges = email != account.Email && email != account.PendingEmail;

        if (emailChanges && !string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            // Before the attempt counter: a missing password is a malformed request, not a guess.
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                throw new DomainException("FieldRequired");
            }

            await EnsureCurrentPasswordAsync(account, request.CurrentPassword, cancellationToken);
        }

        if (emailChanges
            && await repository.FirstOrDefaultAsync(
                candidate => candidate.Email == email && candidate.Id != account.Id,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }

        // The email only ever changes through the confirmed link, so Update always keeps the current one.
        account.Update(new AccountParams(
            request.Name,
            account.Username,
            account.Email,
            request.Phone,
            request.Position,
            request.Address));

        if (emailChanges)
        {
            account.RequestEmailChange(email);
            await emailChangeLink.IssueAsync(account, cancellationToken);
        }
        else
        {
            repository.Update(account);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToProfile(account);
    }

    public async Task<ProfileDto> UploadAvatarAsync(AvatarUploadRequest request, CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        if (request.Size <= 0)
        {
            throw new DomainException(ApplicationMessages.FileIsRequired);
        }

        if (request.Size > MaxAvatarBytes)
        {
            throw new FormattedDomainException(ApplicationMessages.FileSizeExceeded, MaxAvatarBytes);
        }

        byte[] data = await ReadBoundedAsync(request.Content, cancellationToken);
        (string contentType, string extension) = DetectImage(data)
            ?? throw new DomainException(ApplicationMessages.AvatarInvalidImage);

        using MemoryStream content = new(data);
        StorageUploadResult uploaded = await storageService.UploadAsync(
            content, $"avatar{extension}", contentType, cancellationToken, AvatarFolder);

        string? previousPath = account.AvatarPath;
        account.SetAvatar(uploaded.StoragePath);
        repository.Update(account);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(uploaded.StoragePath);
            throw;
        }

        if (previousPath is not null)
        {
            await DeleteQuietlyAsync(previousPath);
        }

        return ToProfile(account);
    }

    public async Task<ProfileDto> DeleteAvatarAsync(CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        string? previousPath = account.AvatarPath;
        if (previousPath is null)
        {
            return ToProfile(account);
        }

        account.SetAvatar(null);
        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DeleteQuietlyAsync(previousPath);

        return ToProfile(account);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
        Account account = await GetCurrentAccountAsync(cancellationToken);

        // No password to guess: nothing counted.
        if (string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        // Same scheme as sign-in (AuthService.LoginAsync), on its own counter: a stolen session can't brute-force
        // the current password here, and can't lock the owner out of signing in either. Reserved and reported
        // before the check, so parallel guesses can't slip past the limit and the lock confirms no guess.
        DateTime utcNow = DateTime.UtcNow;
        int? attempt = await passwordAttemptStore.ReserveAttemptAsync(
            PasswordAttemptPurpose.PasswordChange,
            account.Id,
            utcNow,
            TimeSpan.FromMinutes(lockoutSettings.WindowMinutes),
            cancellationToken);
        if (attempt is not { } attemptNumber || attemptNumber > lockoutSettings.MaxFailedAttempts)
        {
            throw new UnauthorizedException(ApplicationMessages.PasswordChangeTemporarilyLocked);
        }

        if (!passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            if (attemptNumber == lockoutSettings.MaxFailedAttempts)
            {
                // Not the request's token — an aborted request must not skip the lock.
                await passwordAttemptStore.LockAsync(
                    PasswordAttemptPurpose.PasswordChange,
                    account.Id,
                    utcNow.AddMinutes(lockoutSettings.LockDurationMinutes),
                    CancellationToken.None);
            }

            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        await passwordAttemptStore.ClearAsync(PasswordAttemptPurpose.PasswordChange, account.Id, cancellationToken);

        account.SetPasswordHash(passwordHasher.Hash(request.NewPassword));

        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Same scheme as ChangePasswordAsync on the email-change counter: reserved before the check, locked on the last
    // allowed miss with a token that an aborted request cannot cancel, cleared on success.
    private async Task EnsureCurrentPasswordAsync(Account account, string password, CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;
        int? attempt = await passwordAttemptStore.ReserveAttemptAsync(
            PasswordAttemptPurpose.EmailChange,
            account.Id,
            utcNow,
            TimeSpan.FromMinutes(lockoutSettings.WindowMinutes),
            cancellationToken);
        if (attempt is not { } attemptNumber || attemptNumber > lockoutSettings.MaxFailedAttempts)
        {
            throw new UnauthorizedException(ApplicationMessages.EmailChangeTemporarilyLocked);
        }

        if (!passwordHasher.Verify(password, account.PasswordHash!))
        {
            if (attemptNumber == lockoutSettings.MaxFailedAttempts)
            {
                await passwordAttemptStore.LockAsync(
                    PasswordAttemptPurpose.EmailChange,
                    account.Id,
                    utcNow.AddMinutes(lockoutSettings.LockDurationMinutes),
                    CancellationToken.None);
            }

            throw new UnauthorizedException(ApplicationMessages.InvalidCurrentPassword);
        }

        await passwordAttemptStore.ClearAsync(PasswordAttemptPurpose.EmailChange, account.Id, cancellationToken);
    }

    private ProfileDto ToProfile(Account account) =>
        AccountMapper.ToProfileDto(account) with
        {
            AvatarUrl = account.AvatarPath is null ? null : storageSettings.BuildPublicUrl(account.AvatarPath)
        };

    // An orphaned object is only wasted space; it must never fail a request whose data is already saved.
    private async Task DeleteQuietlyAsync(string storagePath)
    {
        try
        {
            await storageService.DeleteAsync(storagePath, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete avatar object {StoragePath}", storagePath);
        }
    }

    // Reads at most one byte over the limit, so a lying Content-Length can't make the server buffer more.
    private static async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxAvatarBytes)
            {
                throw new FormattedDomainException(ApplicationMessages.FileSizeExceeded, MaxAvatarBytes);
            }
        }

        return buffer.ToArray();
    }

    private static (string ContentType, string Extension)? DetectImage(byte[] data)
    {
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("image/png", ".png");
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        if (data.Length >= 12
            && data.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }

    private async Task<Account> GetCurrentAccountAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
    }
}
