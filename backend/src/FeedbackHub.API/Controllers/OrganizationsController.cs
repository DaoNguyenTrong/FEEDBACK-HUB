using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Services.Organizations;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(IOrganizationService organizationService) : ControllerBase
{
    /// <summary>Creates a new organization. The caller, whose email must be confirmed, becomes its Owner.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationDto>> Create(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        OrganizationDto organization = await organizationService.CreateAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, organization);
    }

    /// <summary>Returns the organizations the current account is an active member of, deactivated ones included.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetMine(CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationDto> organizations = await organizationService.ListMineAsync(cancellationToken);

        return Ok(organizations);
    }
}
