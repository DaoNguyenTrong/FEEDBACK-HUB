using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.Auth;

namespace FeedbackHub.Application.Services.Invitations;

public sealed record RegisterWithInvitationResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    AccountDto Account,
    Guid? OrganizationId,
    string? OrganizationName,
    IReadOnlyList<string> Permissions,
    bool IsSystemAdministrator,
    InvitationOutcome InvitationOutcome)
    : LoginResponse(
        AccessToken,
        RefreshToken,
        AccessTokenExpiry,
        Account,
        OrganizationId,
        OrganizationName,
        Permissions,
        IsSystemAdministrator);
