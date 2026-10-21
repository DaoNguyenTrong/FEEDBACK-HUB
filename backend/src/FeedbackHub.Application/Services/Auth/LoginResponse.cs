using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.Application.Services.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    Guid? OrganizationId,
    string? OrganizationName,
    IReadOnlyList<string> Permissions);
