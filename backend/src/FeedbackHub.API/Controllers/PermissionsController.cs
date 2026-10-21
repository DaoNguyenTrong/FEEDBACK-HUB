using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Services.PermissionCatalog;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/permissions")]
public sealed class PermissionsController(IPermissionCatalogService permissionCatalogService) : ControllerBase
{
    /// <summary>Returns the fixed catalog of permissions that roles can be granted.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<IReadOnlyList<PermissionDto>> GetAll()
    {
        return Ok(permissionCatalogService.List());
    }
}
