using FeedbackHub.Application.Services.Auth;

namespace FeedbackHub.Application.Services.Invitations;

/// <summary>What registering through an invitation did with the organization (TECH00 §6.3).</summary>
public enum InvitationOutcome
{
    Joined,

    /// <summary>The organization is suspended: the account exists and the invitation is kept (BR-81 point 6).</summary>
    OrganizationSuspended
}

public sealed record RegisterWithInvitationResult(AuthResult Auth, InvitationOutcome Outcome);
