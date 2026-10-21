using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Services.Context;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
