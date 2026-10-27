namespace FeedbackHub.Application.Services.Notifications;

/// <summary>
/// Query for the caller's own notification list; it declares no <c>Search</c>, because the server would not read it.
/// </summary>
public sealed record NotificationListQuery(
    int PageNumber = 1,
    int PageSize = 10,
    NotificationReadState ReadState = NotificationReadState.All);
