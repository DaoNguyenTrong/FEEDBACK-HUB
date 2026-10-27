using FeedbackHub.Application.Services.Roles;

namespace FeedbackHub.Application.Services.Invitations;

public enum InvitationStatus
{
    Pending,
    Expired
}

/// <param name="Status"><c>Expired</c> from <c>ExpiresAt</c> on; nothing else is stored.</param>
/// <param name="LockReason">Why the caller may not resend, revoke or delete it; <c>null</c> when they may.</param>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    RoleRefDto Role,
    DateTime ExpiresAt,
    InvitationStatus Status,
    bool CanRevokeOrDelete,
    bool CanResend,
    RoleLockReason? LockReason);
