using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Accounts;

/// <summary>
/// Creates, sends, counts and invalidates the confirmation links of a pending email change. Public only because
/// <see cref="AccountService"/> (public) takes it in its constructor.
/// </summary>
public sealed class EmailChangeLink(
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<EmailSettings> emailOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<EmailChangeLink> logger)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly EmailSettings emailSettings = emailOptions.Value;

    public const int MaxMailsPerHour = 5;

    private static readonly TimeSpan CapWindow = TimeSpan.FromHours(1);

    private static readonly EmailVerificationPurpose[] ChangePurposes =
        [EmailVerificationPurpose.ChangeEmail, EmailVerificationPurpose.ConfirmOldEmail];

    /// <summary>
    /// Legacy one-shot wrapper (create, save, send); <see cref="AccountService"/> moves onto the three methods
    /// below and this goes away.
    /// </summary>
    public async Task IssueAsync(Account account, CancellationToken cancellationToken)
    {
        string rawToken = await CreateAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await SendAsync(account, rawToken, cancellationToken);
    }

    /// <summary>
    /// Consumes the account's unused change links, adds a new one for the current stage (stage one:
    /// <c>ConfirmOldEmail</c>, stage two: <c>ChangeEmail</c>) carrying <c>PendingEmail</c>, and marks the account
    /// updated. Does not save and does not send: the caller saves, then calls <see cref="SendAsync"/>.
    /// </summary>
    public async Task<string> CreateAsync(Account account, CancellationToken cancellationToken)
    {
        string pendingEmail = account.PendingEmail
            ?? throw new InvalidOperationException("The account has no pending email change.");
        EmailVerificationPurpose purpose = account.PendingEmailStage switch
        {
            EmailChangeStage.AwaitingCurrentEmail => EmailVerificationPurpose.ConfirmOldEmail,
            EmailChangeStage.AwaitingNewEmail => EmailVerificationPurpose.ChangeEmail,
            _ => throw new InvalidOperationException("The pending email change has no stage."),
        };

        await ConsumeAllAsync(account.Id, ChangePurposes, cancellationToken);

        string rawToken = RandomToken.Create();
        EmailVerificationToken token = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id,
            TokenHash.Compute(rawToken),
            dateTimeProvider.UtcNow.Add(Lifetime),
            purpose,
            pendingEmail));

        unitOfWork.Repository<Account, Guid>().Update(account);
        await unitOfWork.Repository<EmailVerificationToken, Guid>().AddAsync(token, cancellationToken);
        return rawToken;
    }

    /// <summary>
    /// Mails the link made by <see cref="CreateAsync"/>: stage one to the current address, stage two to the
    /// pending one. Call it after the commit; a failure is logged (account id only) and not thrown.
    /// </summary>
    public async Task SendAsync(Account account, string rawToken, CancellationToken cancellationToken)
    {
        string pendingEmail = account.PendingEmail
            ?? throw new InvalidOperationException("The account has no pending email change.");
        bool stageOne = account.PendingEmailStage == EmailChangeStage.AwaitingCurrentEmail;

        // Hash routing, as in RegistrationService: the route must follow "#/".
        string route = stageOne ? "confirm-current-email" : "confirm-email-change";
        string url = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/#/{route}?token={rawToken}";
        string encodedUrl = WebUtility.HtmlEncode(url);
        string encodedPending = WebUtility.HtmlEncode(pendingEmail);
        string recipient;
        string subject;
        string body;
        if (stageOne)
        {
            recipient = account.Email;
            subject = "Xác nhận yêu cầu đổi địa chỉ email";
            body = $"""
                <p>Xin chào {WebUtility.HtmlEncode(account.Name)},</p>
                <p>Tài khoản <strong>{WebUtility.HtmlEncode(account.Username)}</strong> vừa yêu cầu đổi địa chỉ email sang <strong>{encodedPending}</strong>. Mở liên kết bên dưới để xác nhận yêu cầu này:</p>
                <p><a href="{encodedUrl}">{encodedUrl}</a></p>
                <p>Nếu bạn bấm Xác nhận trên trang này, tài khoản sẽ chuyển sang địa chỉ {encodedPending}.</p>
                <p>Link có hiệu lực trong 24 giờ.</p>
                <p>Nếu không phải bạn, hãy bỏ qua thư này.</p>
                """;
        }
        else
        {
            recipient = pendingEmail;
            subject = "Xác nhận đổi địa chỉ email";
            body = $"""
                <p>Xin chào {WebUtility.HtmlEncode(account.Name)},</p>
                <p>Tài khoản <strong>{WebUtility.HtmlEncode(account.Username)}</strong> vừa yêu cầu đổi địa chỉ email sang địa chỉ này. Nhấn vào liên kết bên dưới để xác nhận:</p>
                <p><a href="{encodedUrl}">{encodedUrl}</a></p>
                <p>Link có hiệu lực trong 24 giờ.</p>
                <p>Nếu không phải bạn, hãy bỏ qua thư này.</p>
                """;
        }

        try
        {
            await emailSender.SendAsync(recipient, subject, body, cancellationToken);
        }
        catch (Exception exception)
        {
            // The change and token are saved; the user resends from the profile. Only the account id is logged.
            logger.LogError(exception, "Failed to send email change link to account {AccountId}", account.Id);
        }
    }

    /// <summary>
    /// Throws 429 when <see cref="MaxMailsPerHour"/> or more change links (consumed ones included) were created in
    /// the last hour. Call it under the account lock so a burst cannot slip past.
    /// </summary>
    public async Task EnsureUnderCapAsync(Guid accountId, CancellationToken cancellationToken)
    {
        DateTime since = dateTimeProvider.UtcNow.Subtract(CapWindow);
        IReadOnlyList<EmailVerificationToken> recent = await unitOfWork.Repository<EmailVerificationToken, Guid>()
            .ListAsync(
                token => token.AccountId == accountId
                    && (token.Purpose == EmailVerificationPurpose.ChangeEmail
                        || token.Purpose == EmailVerificationPurpose.ConfirmOldEmail)
                    && token.CreatedAt > since,
                cancellationToken);

        if (recent.Count >= MaxMailsPerHour)
        {
            throw new TooManyRequestsException(ApplicationMessages.EmailChangeTooManyRequests);
        }
    }

    /// <summary>
    /// Consumes the account's unused links of <paramref name="purposes"/> (<c>null</c>: every purpose, verify-email
    /// included). Does not save.
    /// </summary>
    public async Task ConsumeAllAsync(
        Guid accountId,
        IReadOnlyCollection<EmailVerificationPurpose>? purposes,
        CancellationToken cancellationToken)
    {
        IRepository<EmailVerificationToken, Guid> repository = unitOfWork.Repository<EmailVerificationToken, Guid>();
        DateTime utcNow = dateTimeProvider.UtcNow;

        IReadOnlyList<EmailVerificationToken> active = await repository.ListAsync(
            token => token.AccountId == accountId && token.ConsumedAt == null && token.ExpiresAt > utcNow,
            cancellationToken);

        foreach (EmailVerificationToken token in active.Where(t => purposes is null || purposes.Any(p => p == t.Purpose)))
        {
            token.Consume();
            repository.Update(token);
        }
    }
}
