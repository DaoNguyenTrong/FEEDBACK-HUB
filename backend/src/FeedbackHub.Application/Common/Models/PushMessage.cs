namespace FeedbackHub.Application.Common.Models;

public record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);
