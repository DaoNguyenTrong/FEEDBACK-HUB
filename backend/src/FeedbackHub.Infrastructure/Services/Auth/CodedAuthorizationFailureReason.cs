using Microsoft.AspNetCore.Authorization;
using FeedbackHub.Application.Common.Authorization;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>A denial with its own status and code; the base message is the code alone, never the organization name.</summary>
public sealed class CodedAuthorizationFailureReason(
    IAuthorizationHandler handler, int statusCode, string code, string? organizationName = null)
    : AuthorizationFailureReason(handler, code), ICodedAuthorizationFailure
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string? OrganizationName { get; } = organizationName;
}
