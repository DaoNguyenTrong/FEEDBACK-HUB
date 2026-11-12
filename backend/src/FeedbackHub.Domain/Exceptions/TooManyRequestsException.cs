namespace FeedbackHub.Domain.Exceptions;

/// <summary>
/// The caller asked for the same thing too often within a window → 429. The request was fine; a retry after the
/// window succeeds.
/// </summary>
public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message)
        : base(message)
    {
    }
}
