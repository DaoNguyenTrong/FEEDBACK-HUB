using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Organizations;

/// <param name="Name">The new name; blank answers 400 <c>OrganizationNameRequired</c>. Any other field, slug included, is ignored.</param>
public sealed record RenameOrganizationRequest([MaxLength(200, ErrorMessage = "FieldMaxLength")] string? Name);
