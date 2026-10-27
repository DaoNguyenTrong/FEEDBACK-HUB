using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FeedbackHub.API.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Services.Invitations;

namespace FeedbackHub.API.Controllers;

/// <summary>Opening and accepting an invitation link. The code travels only in a JSON body, never in the URL.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.SignedInUser)]
[Route("api/invitations")]
public sealed class InvitationsController(IInvitationService invitationService) : ControllerBase
{
    /// <summary>
    /// What the invitation link shows, without using it up: the first check it fails for the signed-in account, if any.
    /// Without a session (an API key counts as none) a usable invitation is Valid, even in a suspended organization.
    /// </summary>
    /// <remarks>Responses carry `Cache-Control: no-store`, `Referrer-Policy: no-referrer` and `X-Robots-Tag: noindex`. Details appear only as the status allows: Valid has all four, Expired only `expiresAt`, OrganizationSuspended the organization name and `expiresAt`, the others none.</remarks>
    [AllowAnonymous]
    [EnableRateLimiting("invitation")]
    [PrivateLinkResponse]
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(InvitationLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<InvitationLookupDto>> Lookup(
        InvitationTokenRequest request,
        CancellationToken cancellationToken)
    {
        InvitationLookupDto lookup = await invitationService.LookupAsync(request, cancellationToken);

        return Ok(lookup);
    }

    /// <summary>
    /// Joins the signed-in account to the invitation's organization with its role and uses the invitation up; already
    /// an active member gets 200 with no change. The session is not switched: switch-organization does that.
    /// </summary>
    /// <remarks>Refuses with 409 `InvitationNoLongerValid`, `InvitationExpired`, `InvitationWrongAccount`, then 403 `EmailNotConfirmed`, then 409 `InvitationOrganizationSuspended`; a refused invitation is kept.</remarks>
    [EnableRateLimiting("invitation")]
    [HttpPost("accept")]
    [ProducesResponseType(typeof(AcceptInvitationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AcceptInvitationResult>> Accept(
        InvitationTokenRequest request,
        CancellationToken cancellationToken)
    {
        AcceptInvitationResult accepted = await invitationService.AcceptAsync(request, cancellationToken);

        return Ok(accepted);
    }
}
