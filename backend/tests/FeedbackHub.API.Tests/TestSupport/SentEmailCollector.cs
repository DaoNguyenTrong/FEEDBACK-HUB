using System.Collections.Concurrent;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.API.Tests.TestSupport;

public sealed record SentEmail(string ToEmail, string Subject, string HtmlBody);

/// <summary>
/// Everything the host tried to email, in order — a singleton so a test can read back what the caller never
/// sees, such as the verification token, which the server stores only as a hash. Cleared by
/// <see cref="ApiFactoryFixture.ResetAsync"/>.
/// </summary>
public sealed class SentEmailCollector
{
    private readonly ConcurrentQueue<SentEmail> sent = new();
    private int failNext;

    public IReadOnlyList<SentEmail> Sent => [.. sent];

    public void Add(SentEmail email) => sent.Enqueue(email);

    /// <summary>The next send fails as <c>SmtpEmailSender</c> does on an SMTP error or timeout; it records nothing.</summary>
    public void FailNextSend() => Interlocked.Exchange(ref failNext, 1);

    /// <returns>Whether this send was armed to fail; disarms it.</returns>
    public bool TakeFailure() => Interlocked.Exchange(ref failNext, 0) == 1;

    // Also disarms a failure no test consumed, so it cannot leak into the next test.
    public void Clear()
    {
        Interlocked.Exchange(ref failNext, 0);
        sent.Clear();
    }
}

/// <summary>Replaces SMTP in the test host: records the message instead of sending it.</summary>
public sealed class CollectingEmailSender(SentEmailCollector collector) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (collector.TakeFailure())
        {
            throw new ServiceUnavailableException(ApplicationMessages.EmailDeliveryFailed);
        }

        collector.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
