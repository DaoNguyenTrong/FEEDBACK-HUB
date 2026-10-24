using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.Application.Services.Auth;

/// <param name="AccountCreated">True only when this sign-in created the account, so an invitation page can accept
/// on its own (AC-178).</param>
public sealed record ExternalLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    Guid? OrganizationId,
    string? OrganizationName,
    IReadOnlyList<string> Permissions,
    bool IsSystemAdministrator,
    bool AccountCreated)
    : LoginResponse(
        AccessToken,
        RefreshToken,
        AccessTokenExpiry,
        Account,
        OrganizationId,
        OrganizationName,
        Permissions,
        IsSystemAdministrator);
