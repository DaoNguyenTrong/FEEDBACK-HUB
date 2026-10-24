using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.Organizations;

namespace FeedbackHub.API.Controllers;

/// <summary>The organization the caller is working in: the one named by the session, never by the route.</summary>
[ApiController]
[Authorize]
[Route("api/organization")]
public sealed class CurrentOrganizationController(
    IOrganizationContextService organizationContextService,
    IMemberService memberService) : ControllerBase
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

    /// <summary>
    /// Lists the working organization's active members. Emails and per-row actions are shown only to callers who may
    /// manage members; everyone else sees just their own email. Outsiders get 404.
    /// </summary>
    [HttpGet("members")]
    [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMemberOrNotFound)]
    [ProducesResponseType(typeof(PagedResult<OrganizationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<OrganizationMemberDto>>> ListMembers(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<OrganizationMemberDto> members = await memberService.ListAsync(request, cancellationToken);

        return Ok(members);
    }

    /// <summary>
    /// Replaces a member's organization roles. Changing an Owner's roles, or adding or dropping Owner, takes an Owner;
    /// every other role taken away or added must be within the caller's permissions. The same set again: 204, nothing
    /// changes.
    /// </summary>
    [HttpPut("members/{accountId:guid}/roles")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeMemberRoles(
        Guid accountId,
        UpdateMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        await memberService.ChangeRolesAsync(accountId, request, cancellationToken);

        return NoContent();
    }

    /// <summary>Removes a member from the working organization. Callers leave through <c>leave</c>, never here.</summary>
    [HttpDelete("members/{accountId:guid}")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid accountId, CancellationToken cancellationToken)
    {
        await memberService.RemoveAsync(accountId, cancellationToken);

        return NoContent();
    }

    /// <summary>Leaves the working organization. The last active Owner cannot leave.</summary>
    [HttpPost("leave")]
    [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMember)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(CancellationToken cancellationToken)
    {
        await memberService.LeaveAsync(cancellationToken);

        return NoContent();
    }
}
