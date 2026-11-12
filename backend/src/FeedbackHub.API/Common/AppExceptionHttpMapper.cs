using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.API.Common;

internal static class AppExceptionHttpMapper
{
    public static (int StatusCode, string Title) Map(AppException exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        DomainException or FormattedDomainException => (StatusCodes.Status400BadRequest, "Bad Request"),
        ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable"),
        TooManyRequestsException => (StatusCodes.Status429TooManyRequests, "Too Many Requests"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
    };
}
