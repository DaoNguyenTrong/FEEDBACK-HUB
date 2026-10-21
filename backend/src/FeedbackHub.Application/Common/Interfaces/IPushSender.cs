using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Common.Interfaces;

public interface IPushSender
{
    Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken);
}
