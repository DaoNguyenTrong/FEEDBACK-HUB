namespace FeedbackHub.Application.Services.Organizations;

public sealed record CurrentOrganizationDto(Guid Id, string Name, string Slug, bool Status);
