using System.Diagnostics;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Infrastructure.Services.Email;

internal sealed class SmtpEmailSender(
    IOptions<EmailSettings> emailOptions,
    ILogger<SmtpEmailSender> logger,
    ISmtpClientFactory smtpClientFactory) : IEmailSender
{
    internal static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(5);

    private readonly EmailSettings emailSettings = emailOptions.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        using ISmtpClient client = smtpClientFactory.Create();

        // One deadline for connect + authenticate + send together, not per step.
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan total = TimeSpan.FromSeconds(emailSettings.SendTimeoutSeconds);
        deadline.CancelAfter(total);
        long startedAt = Stopwatch.GetTimestamp();

        // The socket timeout is what is left of the deadline, so a slow step can't leave the next one a full timeout.
        void LimitToRemainingTime() =>
            client.Timeout = (int)Math.Max(1, (total - Stopwatch.GetElapsedTime(startedAt)).TotalMilliseconds);

        LimitToRemainingTime();

        try
        {
            // Parsing can throw a FormatException whose message echoes the address, so it stays inside the try.
            MimeMessage message = new();
            message.From.Add(new MailboxAddress(emailSettings.FromName, emailSettings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            LimitToRemainingTime();
            await client.ConnectAsync(
                emailSettings.Host,
                emailSettings.Port,
                EmailSecureSocket.Resolve(emailSettings),
                deadline.Token);

            if (!string.IsNullOrWhiteSpace(emailSettings.Username))
            {
                LimitToRemainingTime();
                await client.AuthenticateAsync(emailSettings.Username, emailSettings.Password, deadline.Token);
            }

            LimitToRemainingTime();
            await client.SendAsync(message, deadline.Token);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // A caller cancel can surface as a non-OCE exception (e.g. a socket error): still a cancel, not a 503.
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception exception)
        {
            // MailKit messages often carry the mailbox ("5.1.1 <user@x>: Recipient address rejected"), so neither
            // the exception object nor its message is logged or rethrown: only the type and SMTP codes.
            if (exception is SmtpCommandException commandException)
            {
                logger.LogError(
                    "Failed to send email ({ExceptionType}, status {StatusCode}, error {ErrorCode})",
                    exception.GetType().Name, (int)commandException.StatusCode, commandException.ErrorCode);
            }
            else
            {
                logger.LogError("Failed to send email ({ExceptionType})", exception.GetType().Name);
            }

            throw new ServiceUnavailableException(ApplicationMessages.EmailDeliveryFailed);
        }
        finally
        {
            if (client.IsConnected)
            {
                using CancellationTokenSource disconnectDeadline = new(DisconnectTimeout);
                try
                {
                    await client.DisconnectAsync(true, disconnectDeadline.Token);
                }
                catch (Exception)
                {
                    // Best effort: the send outcome is already decided.
                }
            }
        }
    }
}
