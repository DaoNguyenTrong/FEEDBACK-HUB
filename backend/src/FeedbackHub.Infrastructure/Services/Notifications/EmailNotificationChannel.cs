using Microsoft.Extensions.Logging;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.Notifications;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Infrastructure.Services.Notifications;

internal sealed class EmailNotificationChannel(
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public string Name => "Email";

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        (string Subject, string HtmlBody)? content =
            NotificationEmailTemplates.TryRender(notification.Type, notification.Data);

        if (content is null)
        {
            logger.LogDebug(
                "No email template registered for notification type {Type} — skipping email channel.",
                notification.Type);
            return;
        }

        Account? account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(notification.AccountId, cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Account {AccountId} not found — skipping email for notification {NotificationId}",
                notification.AccountId, notification.Id);
            return;
        }

        await emailSender.SendAsync(account.Email, content.Value.Subject, content.Value.HtmlBody, cancellationToken);
    }
}
