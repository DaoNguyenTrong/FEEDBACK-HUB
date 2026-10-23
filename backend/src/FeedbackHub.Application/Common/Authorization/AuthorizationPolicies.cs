namespace FeedbackHub.Application.Common.Authorization;

/// <summary>
/// Well-known policy names for checks that aren't keyed by a <see cref="Permissions"/>
/// code (e.g. "any active member, no specific permission required") — referenced from
/// <c>FeedbackHub.API</c> controllers via <c>[Authorize(Policy = AuthorizationPolicies.X)]</c>.
/// </summary>
public static class AuthorizationPolicies
{
    public const string OrganizationMember = "OrganizationMember";

    /// <summary>Any active member of the caller's active organization (JWT <c>org_id</c> claim).</summary>
    public const string ActiveOrganizationMember = "ActiveOrganizationMember";

    /// <summary>
    /// Like <see cref="ActiveOrganizationMember"/>, but a caller outside the organization gets 404 instead of 403, for
    /// data read inside an organization (BR-45).
    /// </summary>
    public const string ActiveOrganizationMemberOrNotFound = "ActiveOrganizationMemberOrNotFound";

    /// <summary>
    /// A signed-in person (JWT from the header or the <c>access_token</c> cookie) — never an API key.
    /// <c>[Authorize(AuthenticationSchemes = ...)]</c> can't express this: ASP.NET Core adds the default
    /// policy's schemes (JWT and API key) to it.
    /// </summary>
    public const string SignedInUser = "SignedInUser";
}
