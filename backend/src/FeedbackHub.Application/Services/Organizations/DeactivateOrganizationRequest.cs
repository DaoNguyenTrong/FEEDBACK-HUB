using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="Reason">Why it is deactivated. Required for a system administrator: always on API-25, on API-05 only
/// when the caller is one (blank answers 400 <c>DeactivationReasonRequired</c>). Optional for a member on API-05.
/// </param>
public sealed record DeactivateOrganizationRequest([MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Reason);
