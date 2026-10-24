using FeedbackHub.Application.Services.Roles;

namespace FeedbackHub.Application.Services.Invitations;

/// <summary>The first BR-81 check an invitation link fails, in the order of TECH00 §6.3.</summary>
public enum InvitationLookupStatus
{
    Valid,
    Expired,
    NoLongerValid,
    WrongAccount,
    EmailNotConfirmed,
    OrganizationSuspended
}

/// <summary>
/// Only what the status allows (TECH00 §6.3, D10): <c>Valid</c> carries all four details, <c>Expired</c> only
/// <c>ExpiresAt</c>, <c>OrganizationSuspended</c> the organization name and <c>ExpiresAt</c>, the others nothing.
/// </summary>
public sealed record InvitationLookupDto(
    InvitationLookupStatus Status,
    string? OrganizationName,
    RoleRefDto? Role,
    DateTime? ExpiresAt,
    string? Email);
