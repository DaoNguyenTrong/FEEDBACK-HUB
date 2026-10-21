namespace FeedbackHub.Domain.Exceptions;

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
