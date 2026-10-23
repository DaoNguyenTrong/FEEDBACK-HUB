using System.Diagnostics;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Services.Email;

namespace FeedbackHub.Infrastructure.Tests.Services.Email;

public class SmtpEmailSenderTests
{
    private sealed record Fixture(SmtpEmailSender Sender, ISmtpClient Client, CapturingLogger Logger);

    private sealed class CapturingLogger : ILogger<SmtpEmailSender>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Everything a sink could render: formatted text, structured values, the exception and its chain.
            Entries.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.AddRange(values.Select(v => $"{v.Key}={v.Value}"));
            }

            if (exception is not null)
            {
                Entries.Add(exception.ToString());
            }
        }
    }

    private static Fixture CreateFixture(string username = "", bool useSsl = false, int sendTimeoutSeconds = 15)
    {
        IOptions<EmailSettings> options = Options.Create(new EmailSettings
        {
            Host = "smtp.example.com",
            Port = 587,
            Username = username,
            Password = "password",
            FromAddress = "noreply@example.com",
            FromName = "FeedbackHub",
            UseSsl = useSsl,
            SendTimeoutSeconds = sendTimeoutSeconds
        });

        ISmtpClient client = Substitute.For<ISmtpClient>();
        ISmtpClientFactory clientFactory = Substitute.For<ISmtpClientFactory>();
        clientFactory.Create().Returns(client);

        CapturingLogger logger = new();
        SmtpEmailSender sender = new(options, logger, clientFactory);

        return new Fixture(sender, client, logger);
    }

    [Fact]
    public async Task SendAsync_ConnectsWithConfiguredHostAndPort()
    {
        Fixture f = CreateFixture();

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).ConnectAsync(
            "smtp.example.com", 587, Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UseSslTrue_ConnectsWithSslOnConnect()
    {
        Fixture f = CreateFixture(useSsl: true);

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).ConnectAsync(
            Arg.Any<string>(), Arg.Any<int>(), SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_BlankUsername_SkipsAuthenticate()
    {
        Fixture f = CreateFixture(username: "");

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_NonBlankUsername_Authenticates()
    {
        Fixture f = CreateFixture(username: "smtp-user");

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).AuthenticateAsync("smtp-user", "password", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SendsMessageWithSubjectAndRecipient()
    {
        Fixture f = CreateFixture();

        await f.Sender.SendAsync("to@example.com", "Welcome", "<p>Hello</p>", CancellationToken.None);

        await f.Client.Received(1).SendAsync(
            Arg.Is<MimeMessage>(m => m != null
                && m.Subject == "Welcome"
                && m.To.Mailboxes.Any(mb => mb.Address == "to@example.com")),
            Arg.Any<CancellationToken>(),
            Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_Connected_DisconnectsInFinally()
    {
        Fixture f = CreateFixture();
        f.Client.IsConnected.Returns(true);

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SendFails_DisconnectsAndRethrows()
    {
        Fixture f = CreateFixture();
        f.Client.IsConnected.Returns(true);
        f.Client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<string>(_ => throw new InvalidOperationException("smtp failure"));

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None));

        await f.Client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    private const string Recipient = "secret.person@example.com";

    private static Task Hang(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);

    private static void HangAtConnect(ISmtpClient client, int delayMs = 0) =>
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(delayMs, CancellationToken.None);
                await Hang(call.Arg<CancellationToken>());
            });

    private static void HangAtAuthenticate(ISmtpClient client, int delayMs = 0) =>
        client.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(delayMs, CancellationToken.None);
                await Hang(call.Arg<CancellationToken>());
            });

    private static void HangAtSend(ISmtpClient client) =>
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(async call =>
            {
                await Hang(call.Arg<CancellationToken>());
                return string.Empty;
            });

    private static async Task AssertStopsWithinTotalTimeout(Fixture f)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        ServiceUnavailableException exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.Sender.SendAsync(Recipient, "Subject", "<p>Body</p>", CancellationToken.None));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"took {stopwatch.Elapsed}");
        Assert.Equal(ApplicationMessages.EmailDeliveryFailed, exception.Message);
    }

    [Fact]
    public async Task SendAsync_Failure_LogsWithoutRecipient()
    {
        Fixture f = CreateFixture();
        f.Client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<string>(_ => throw new SmtpCommandException(
                SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, $"5.1.1 <{Recipient}>: Recipient address rejected"));

        ServiceUnavailableException exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.Sender.SendAsync(Recipient, "Subject", "<p>Body</p>", CancellationToken.None));

        Assert.NotEmpty(f.Logger.Entries);
        Assert.All(f.Logger.Entries, entry => Assert.DoesNotContain(Recipient, entry, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(f.Logger.Entries, entry => entry.Contains("Failed to send email"));
        Assert.DoesNotContain(Recipient, exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task SendAsync_UnparseableRecipient_DoesNotLeakAddress()
    {
        Fixture f = CreateFixture();
        const string badAddress = "secret<<@@>>";

        ServiceUnavailableException exception = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.Sender.SendAsync(badAddress, "Subject", "<p>Body</p>", CancellationToken.None));

        Assert.All(f.Logger.Entries, entry => Assert.DoesNotContain("secret", entry));
        Assert.DoesNotContain("secret", exception.ToString());
    }

    [Fact]
    public async Task SendAsync_CallerCancels_PropagatesOperationCanceled()
    {
        Fixture f = CreateFixture();
        HangAtConnect(f.Client);
        using CancellationTokenSource caller = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => f.Sender.SendAsync(Recipient, "Subject", "<p>Body</p>", caller.Token));
    }

    [Fact]
    public async Task SendAsync_Disconnect_DoesNotUseCallerToken()
    {
        Fixture f = CreateFixture();
        f.Client.IsConnected.Returns(true);
        using CancellationTokenSource caller = new();

        await f.Sender.SendAsync(Recipient, "Subject", "<p>Body</p>", caller.Token);

        await f.Client.Received(1).DisconnectAsync(
            true, Arg.Is<CancellationToken>(t => t != caller.Token && t.CanBeCanceled));
    }

    [Fact]
    public async Task SendAsync_HangsAtConnect_StopsWithinTotalTimeout()
    {
        Fixture f = CreateFixture(sendTimeoutSeconds: 1);
        HangAtConnect(f.Client);

        await AssertStopsWithinTotalTimeout(f);
    }

    [Fact]
    public async Task SendAsync_HangsAtAuthenticate_StopsWithinTotalTimeout()
    {
        Fixture f = CreateFixture(username: "smtp-user", sendTimeoutSeconds: 1);
        HangAtAuthenticate(f.Client);

        await AssertStopsWithinTotalTimeout(f);
    }

    [Fact]
    public async Task SendAsync_HangsAtSend_StopsWithinTotalTimeout()
    {
        Fixture f = CreateFixture(sendTimeoutSeconds: 1);
        HangAtSend(f.Client);

        await AssertStopsWithinTotalTimeout(f);
    }

    [Fact]
    public async Task SendAsync_HangsAcrossSteps_TotalNotPerStep()
    {
        // connect 0.6 s + authenticate hang: a per-step 1 s deadline would end at 1.6 s, one shared deadline at 1 s.
        Fixture f = CreateFixture(username: "smtp-user", sendTimeoutSeconds: 1);
        f.Client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.Delay(600));
        HangAtAuthenticate(f.Client);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => f.Sender.SendAsync(Recipient, "Subject", "<p>Body</p>", CancellationToken.None));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5), $"took {stopwatch.Elapsed}");
    }
}
