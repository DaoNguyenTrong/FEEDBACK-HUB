using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Interfaces;

public interface INotificationChannel
{
    string Name { get; }

    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
