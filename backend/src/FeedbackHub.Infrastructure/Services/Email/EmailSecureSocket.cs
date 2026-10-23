using MailKit.Security;
using FeedbackHub.Application.Common.Settings;

namespace FeedbackHub.Infrastructure.Services.Email;

/// <summary>The accepted <c>EmailSettings:SecureSocket</c> values. Public so the API can validate them in production.</summary>
public static class EmailSecureSocket
{
    public const string SslOnConnect = "SslOnConnect";

    public const string StartTls = "StartTls";

    public const string StartTlsWhenAvailable = "StartTlsWhenAvailable";

    /// <summary>Case-insensitive. Empty falls back to <c>UseSsl</c>; an unknown value throws.</summary>
    public static SecureSocketOptions Resolve(EmailSettings settings)
    {
        string value = settings.SecureSocket?.Trim() ?? string.Empty;

        if (value.Length == 0)
            return settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

        if (string.Equals(value, SslOnConnect, StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.SslOnConnect;

        if (string.Equals(value, StartTls, StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.StartTls;

        if (string.Equals(value, StartTlsWhenAvailable, StringComparison.OrdinalIgnoreCase))
            return SecureSocketOptions.StartTlsWhenAvailable;

        throw new InvalidOperationException(
            $"EmailSettings:SecureSocket must be {SslOnConnect}, {StartTls} or {StartTlsWhenAvailable}.");
    }
}
