namespace FeedbackHub.Application.Common.Authorization;

/// <summary>
/// A policy denial that carries the response to write. The API reads it through this interface because it may not
/// name Infrastructure types outside its composition root.
/// </summary>
public interface ICodedAuthorizationFailure
{
    int StatusCode { get; }

    /// <summary>The message key, written as the problem's <c>code</c>.</summary>
    string Code { get; }

    /// <summary>Written as the top-level <c>organizationName</c> member when set.</summary>
    string? OrganizationName { get; }
}
