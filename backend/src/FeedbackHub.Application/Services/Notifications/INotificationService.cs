using FeedbackHub.Application.Common.Models;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Notifications;

public interface INotificationService
{
    /// <summary>Single-recipient form of <see cref="NotifyManyAsync"/> — same dedupe, same write path.</summary>
    Task NotifyAsync(NotificationParams request, CancellationToken cancellationToken);

    /// <summary>
    /// One save for all broadcasts; skips recipients already holding the <see cref="NotificationBroadcast.DedupeKey"/>
    /// (null: no dedupe). Racing runs may both pass it; the unique index fails the later save, whose retry re-checks.
    /// </summary>
    /// <returns>How many notifications were actually created.</returns>
    Task<int> NotifyManyAsync(IReadOnlyCollection<NotificationBroadcast> broadcasts, CancellationToken cancellationToken);

    Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(
        NotificationListQuery query, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken);
}
