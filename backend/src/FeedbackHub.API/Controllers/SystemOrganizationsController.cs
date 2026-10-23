using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.SystemOrganizations;

namespace FeedbackHub.API.Controllers;

/// <summary>Organizations as a system administrator sees them: name, member count and state, never their data.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.SystemAdministrator)]
[Route("api/system/organizations")]
public sealed class SystemOrganizationsController(ISystemOrganizationService systemOrganizationService) : ControllerBase
{
    /// <summary>Lists every organization by name, ignoring case and accents.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SystemOrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SystemOrganizationDto>>> List(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<SystemOrganizationDto> page = await systemOrganizationService.ListAsync(request, cancellationToken);

        return Ok(page);
    }

    /// <summary>Deactivates an organization. Already deactivated: 204, nothing changes.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid id,
        DeactivateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        await systemOrganizationService.DeactivateAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Reactivates an organization with its members and roles intact. Already active: 204.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        await systemOrganizationService.ReactivateAsync(id, cancellationToken);

        return NoContent();
    }
}
