namespace FeedbackHub.Application.Services.Invitations;

/// <summary>The organization just joined, so the client can switch into it.</summary>
public sealed record AcceptInvitationResult(Guid OrganizationId, string OrganizationName);
