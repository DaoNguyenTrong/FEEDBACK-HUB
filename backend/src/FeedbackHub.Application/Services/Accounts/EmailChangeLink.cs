using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Accounts;

/// <summary>
/// Issues and invalidates the confirmation links of a pending email change. Public only because
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

    /// <summary>
    /// Invalidates the account's earlier change links, stores a new one for <c>PendingEmail</c>, saves, then
    /// mails it. The caller must have set <c>PendingEmail</c>; the save here also persists that change.
    /// </summary>
    public async Task IssueAsync(Account account, CancellationToken cancellationToken)
    {
        string pendingEmail = account.PendingEmail
            ?? throw new InvalidOperationException("The account has no pending email change.");

        await ConsumeAllAsync(account.Id, EmailVerificationPurpose.ChangeEmail, cancellationToken);

        string rawToken = RandomToken.Create();
        EmailVerificationToken token = EmailVerificationToken.Create(new EmailVerificationTokenParams(
            account.Id,
            TokenHash.Compute(rawToken),
            dateTimeProvider.UtcNow.Add(Lifetime),
            EmailVerificationPurpose.ChangeEmail,
            pendingEmail));

        unitOfWork.Repository<Account, Guid>().Update(account);
        await unitOfWork.Repository<EmailVerificationToken, Guid>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Hash routing, as in RegistrationService: the route must follow "#/".
        string url = $"{emailSettings.FrontendBaseUrl.TrimEnd('/')}/#/confirm-email-change?token={rawToken}";
        string encodedUrl = WebUtility.HtmlEncode(url);
        string body = $"""
            <p>Xin chào {WebUtility.HtmlEncode(account.Name)},</p>
            <p>Tài khoản <strong>{WebUtility.HtmlEncode(account.Username)}</strong> vừa yêu cầu đổi địa chỉ email sang địa chỉ này. Nhấn vào liên kết bên dưới để xác nhận:</p>
            <p><a href="{encodedUrl}">{encodedUrl}</a></p>
            <p>Link có hiệu lực trong 24 giờ.</p>
            <p>Nếu không phải bạn, hãy bỏ qua thư này.</p>
            """;

        try
        {
            await emailSender.SendAsync(pendingEmail, "Xác nhận đổi địa chỉ email", body, cancellationToken);
        }
        catch (Exception exception)
        {
            // Pending address and token are saved; the user resends from the profile. Only the account id is logged.
            logger.LogError(exception, "Failed to send email change link to account {AccountId}", account.Id);
        }
    }

    /// <summary>Consumes the account's unused links of <paramref name="purpose"/> (<c>null</c>: any). Does not save.</summary>
    public async Task ConsumeAllAsync(
        Guid accountId,
        EmailVerificationPurpose? purpose,
        CancellationToken cancellationToken)
    {
        IRepository<EmailVerificationToken, Guid> repository = unitOfWork.Repository<EmailVerificationToken, Guid>();
        DateTime utcNow = dateTimeProvider.UtcNow;

        IReadOnlyList<EmailVerificationToken> active = await repository.ListAsync(
            token => token.AccountId == accountId && token.ConsumedAt == null && token.ExpiresAt > utcNow,
            cancellationToken);

        foreach (EmailVerificationToken token in active.Where(t => purpose is null || t.Purpose == purpose))
        {
            token.Consume();
            repository.Update(token);
        }
    }
}
