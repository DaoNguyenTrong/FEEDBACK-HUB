using MailKit.Security;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure.Services.Email;

namespace FeedbackHub.Infrastructure.Tests.Services.Email;

public class EmailSecureSocketTests
{
    [Fact]
    public void Resolve_Empty_UseSslTrue_IsSslOnConnect()
    {
        Assert.Equal(SecureSocketOptions.SslOnConnect, EmailSecureSocket.Resolve(new EmailSettings { UseSsl = true }));
    }

    [Fact]
    public void Resolve_Empty_UseSslFalse_IsStartTlsWhenAvailable()
    {
        Assert.Equal(SecureSocketOptions.StartTlsWhenAvailable, EmailSecureSocket.Resolve(new EmailSettings { UseSsl = false }));
    }

    [Theory]
    [InlineData("SslOnConnect", SecureSocketOptions.SslOnConnect)]
    [InlineData("sslonconnect", SecureSocketOptions.SslOnConnect)]
    [InlineData("StartTls", SecureSocketOptions.StartTls)]
    [InlineData("STARTTLS", SecureSocketOptions.StartTls)]
    [InlineData("StartTlsWhenAvailable", SecureSocketOptions.StartTlsWhenAvailable)]
    [InlineData("starttlswhenavailable", SecureSocketOptions.StartTlsWhenAvailable)]
    public void Resolve_ExplicitValuesIgnoringCase_MapToMailKitOptions(string value, SecureSocketOptions expected)
    {
        // UseSsl contradicts the explicit value on purpose: the explicit value wins.
        Assert.Equal(expected, EmailSecureSocket.Resolve(new EmailSettings { SecureSocket = value, UseSsl = expected != SecureSocketOptions.SslOnConnect }));
    }

    [Fact]
    public void Resolve_Unknown_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => EmailSecureSocket.Resolve(new EmailSettings { SecureSocket = "Tls13" }));
    }
}
