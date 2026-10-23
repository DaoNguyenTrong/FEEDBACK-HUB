namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>How the organization gate answers a caller outside the organization (TECH00 §5.1 step 2).</summary>
public enum DenyMode
{
    /// <summary>403 <c>OrganizationAccessDenied</c>.</summary>
    Forbidden = 1,

    /// <summary>404 <c>ResourceNotFound</c>, so a former member cannot tell the data apart from data that never existed.</summary>
    NotFound = 2
}
