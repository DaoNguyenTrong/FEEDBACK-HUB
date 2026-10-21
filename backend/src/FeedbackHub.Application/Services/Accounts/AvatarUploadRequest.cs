namespace FeedbackHub.Application.Services.Accounts;

public sealed record AvatarUploadRequest(Stream Content, string ContentType, long Size);
