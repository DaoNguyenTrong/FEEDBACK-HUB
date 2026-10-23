namespace FeedbackHub.Application.Services.SystemOrganizations;

/// <param name="MemberCount">Active members only.</param>
public sealed record SystemOrganizationDto(Guid Id, string Name, int MemberCount, bool IsDeactivated);
