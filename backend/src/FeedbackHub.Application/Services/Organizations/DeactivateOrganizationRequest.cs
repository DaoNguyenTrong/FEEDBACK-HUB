using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="Reason">Why the organization is deactivated; required, blank answers 400 <c>DeactivationReasonRequired</c>.</param>
public sealed record DeactivateOrganizationRequest([MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Reason);
