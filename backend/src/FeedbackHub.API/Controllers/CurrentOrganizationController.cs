using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Services.Organizations;

namespace FeedbackHub.API.Controllers;

/// <summary>The organization the caller is working in: the one named by the session, never by the route.</summary>
[ApiController]
[Authorize]
[Route("api/organization")]
public sealed class CurrentOrganizationController(IOrganizationContextService organizationContextService)
    : ControllerBase
{
    /// <summary>Returns the working organization. Any active member may read it.</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMember)]
    [ProducesResponseType(typeof(CurrentOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrentOrganizationDto>> Get(CancellationToken cancellationToken)
    {
        CurrentOrganizationDto organization = await organizationContextService.GetAsync(cancellationToken);

        return Ok(organization);
    }

    /// <summary>Renames the working organization; its slug never changes. The same name again: 200, nothing changes.</summary>
    [HttpPatch]
    [Authorize(Policy = Permissions.OrganizationManage)]
    [ProducesResponseType(typeof(CurrentOrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrentOrganizationDto>> Rename(
        RenameOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        CurrentOrganizationDto organization = await organizationContextService.RenameAsync(request, cancellationToken);

        return Ok(organization);
    }

    /// <summary>Deactivates the working organization; every member loses access on their next request.</summary>
    [HttpPost("deactivate")]
    [Authorize(Policy = Permissions.OrganizationManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        DeactivateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        await organizationContextService.DeactivateAsync(request, cancellationToken);

        return NoContent();
    }
}
