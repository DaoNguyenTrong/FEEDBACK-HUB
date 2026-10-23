using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Auth;

public sealed class RegistrationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailOptions,
    ITokenIssuer tokenIssuer,
    EmailChangeLink emailChangeLink,
    IDateTimeProvider clock,
    ILogger<RegistrationService> logger) : IRegistrationService
{
    private readonly EmailSettings emailSettings = emailOptions.Value;

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();

        await EnsureUniqueAccountAsync(
            accountRepository,
            Account.NormalizeUsername(request.Username),
            Account.NormalizeEmail(request.Email),
            cancellationToken);

        Account account = Account.Create(request.ToParams());
        account.SetPasswordHash(passwordHasher.Hash(request.Password));

        await accountRepository.AddAsync(account, cancellationToken);

        await IssueVerificationTokenAsync(account, cancellationToken);

        return new RegisterResult(account.Id, account.Email);
    }

    public async Task<AuthResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        string? deviceInfo,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        string tokenHash = TokenHash.Compute(request.Token);

        IRepository<EmailVerificationToken, Guid> tokenRepository =
            unitOfWork.Repository<EmailVerificationToken, Guid>();

        EmailVerificationToken? token = await tokenRepository
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        // A null purpose is a row from before purposes existed; it is still a verification token.
        if (token is null
            || !token.IsActiveAt(clock.UtcNow)
            || token.Purpose is not (null or EmailVerificationPurpose.VerifyEmail))
        {
            throw new UnauthorizedException(ApplicationMessages.EmailVerificationTokenInvalidOrExpired);
        }

        Account account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(token.AccountId, cancellationToken)
            ?? throw new UnauthorizedException(ApplicationMessages.EmailVerificationTokenInvalidOrExpired);

        // The token proves control of the address it was mailed to; it must still be the account's address.
        // A legacy row without an address cannot prove that, so it is refused.
        if (token.Email is null || token.Email != account.Email)
        {
            throw new UnauthorizedException(ApplicationMessages.EmailVerificationTokenInvalidOrExpired);
        }

        account.ConfirmEmail();
        unitOfWork.Repository<Account, Guid>().Update(account);

        token.Consume();
        tokenRepository.Update(token);

        Guid? organizationId = await tokenIssuer.ResolveDefaultOrganizationIdAsync(account.Id, cancellationToken);

        return await tokenIssuer.IssueTokensAsync(account, organizationId, deviceInfo, ipAddress, false, DateTime.UtcNow, familyId: null, cancellationToken);
    }

    public async Task ResendVerificationEmailAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        string email = Account.NormalizeEmail(request.Email);
        Account? account = await unitOfWork.Repository<Account, Guid>()
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

        // An account without a password was made through a provider or an invitation and has nothing to verify here.
        if (account is null || account.PasswordHash is null)
        {
            return;
        }

        if (account.EmailConfirmed)
        {
            throw new ConflictException(ApplicationMessages.EmailAlreadyConfirmed);
        }

        await IssueVerificationTokenAsync(account, cancellationToken);
    }

    public async Task ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        string tokenHash = TokenHash.Compute(request.Token);

        IRepository<EmailVerificationToken, Guid> tokenRepository =
            unitOfWork.Repository<EmailVerificationToken, Guid>();
        IRepository<Account, Guid> accountRepository = unitOfWork.Repository<Account, Guid>();

        EmailVerificationToken? token = await tokenRepository
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null
            || !token.IsActiveAt(clock.UtcNow)
            || token.Purpose != EmailVerificationPurpose.ChangeEmail
            || token.Email is null)
        {
            throw new ConflictException(ApplicationMessages.EmailChangeLinkNoLongerValid);
        }

        Account account = await accountRepository.GetByIdAsync(token.AccountId, cancellationToken)
            ?? throw new ConflictException(ApplicationMessages.EmailChangeLinkNoLongerValid);

        if (account.PendingEmail != token.Email)
        {
            throw new ConflictException(ApplicationMessages.EmailChangeLinkNoLongerValid);
        }

        string newEmail = account.PendingEmail;
        Guid accountId = account.Id;

        // Someone may have registered the address while the change was waiting. The unique index on Email
        // backstops two confirmations racing past this check (mapped in UnitOfWork.SaveChangesAsync).
        if (await accountRepository.FirstOrDefaultAsync(
                a => a.Email == newEmail && a.Id != accountId, cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }

        account.ApplyEmailChange();
        accountRepository.Update(account);

        // Consumes this link too: it is one of the account's active tokens.
        await emailChangeLink.ConsumeAllAsync(account.Id, purpose: null, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUniqueAccountAsync(
        IRepository<Account, Guid> accountRepository,
        string username,
        string email,
        CancellationToken cancellationToken)
    {
        if (await accountRepository.FirstOrDefaultAsync(
                account => account.Username == username,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountUsernameAlreadyExists);
        }

        if (await accountRepository.FirstOrDefaultAsync(
                account => account.Email == email,
                cancellationToken) is not null)
        {
            throw new ConflictException(ApplicationMessages.AccountEmailAlreadyExists);
        }
    }

    private async Task IssueVerificationTokenAsync(Account account, CancellationToken cancellationToken)
    {
        string rawToken = GenerateRawToken();
        string tokenHash = TokenHash.Compute(rawToken);
        DateTime expiresAt = clock.UtcNow.AddHours(emailSettings.VerificationTokenExpiryHours);

        EmailVerificationToken token = EmailVerificationToken.Create(
            new EmailVerificationTokenParams(
                account.Id, tokenHash, expiresAt, EmailVerificationPurpose.VerifyEmail, account.Email));

        await unitOfWork.Repository<EmailVerificationToken, Guid>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The frontend uses hash routing (createWebHashHistory()): a plain path resolves to "/", whose requiresAuth
        // guard bounces the not-yet-signed-in user to /login instead of VerifyEmailView, so the route must follow "#/".
        string verificationUrl = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/#/verify-email?token={rawToken}";
        // The name is whatever the registrant typed — and anyone can register with someone else's address —
        // so it is encoded like every other value placed in mail HTML (see NotificationEmailTemplates).
        string encodedUrl = WebUtility.HtmlEncode(verificationUrl);
        string body = $"""
            <p>Xin chào {WebUtility.HtmlEncode(account.Name)},</p>
            <p>Nhấn vào liên kết bên dưới để xác thực địa chỉ email của bạn:</p>
            <p><a href="{encodedUrl}">{encodedUrl}</a></p>
            <p>Liên kết có hiệu lực trong {emailSettings.VerificationTokenExpiryHours} giờ.</p>
            """;

        try
        {
            await emailSender.SendAsync(account.Email, "Xác thực địa chỉ email", body, cancellationToken);
        }
        catch (Exception exception)
        {
            // Account + token đã commit — lỗi gửi mail (SMTP chưa cấu hình/đang lỗi) không được làm
            // RegisterAsync/ResendVerificationEmailAsync lỗi phía caller, nếu không đăng ký lại cùng username sẽ gặp
            // 409 "đã tồn tại" không lối thoát. User dùng "Gửi lại email xác thực" để thử lại khi SMTP ổn.
            logger.LogError(exception, "Failed to send verification email to account {AccountId}", account.Id);
        }
    }

    private static string GenerateRawToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
