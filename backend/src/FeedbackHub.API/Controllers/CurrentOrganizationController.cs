using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.Roles;

namespace FeedbackHub.API.Controllers;

/// <summary>The organization the caller is working in: the one named by the session, never by the route.</summary>
[ApiController]
[Authorize]
[Route("api/organization")]
public sealed class CurrentOrganizationController(
    IOrganizationContextService organizationContextService,
    IMemberService memberService,
    IRoleService roleService,
    IInvitationService invitationService,
    IAuditLogService auditLogService) : ControllerBase
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

    /// <summary>Lists the working organization's pending and expired invitations, latest expiry first.</summary>
    [HttpGet("invitations")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(typeof(PagedResult<InvitationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InvitationDto>>> ListInvitations(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<InvitationDto> invitations = await invitationService.ListAsync(request, cancellationToken);

        return Ok(invitations);
    }

    /// <summary>
    /// Invites an address with a role, replacing its earlier invitation. The caller must be able to grant the new role
    /// and take away the earlier one. The mail is sent before the commit: when it fails, nothing changes (503).
    /// </summary>
    [HttpPost("invitations")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(typeof(InvitationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvitationDto>> SendInvitation(
        SendInvitationRequest request,
        CancellationToken cancellationToken)
    {
        InvitationDto invitation = await invitationService.SendAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, invitation);
    }

    /// <summary>
    /// Replaces the invitation with a new one to the same address and role (new id, fresh expiry) and mails it. The
    /// caller must be able to grant its role. When the mail fails, nothing changes (503).
    /// </summary>
    [HttpPost("invitations/{id:guid}/resend")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(typeof(InvitationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvitationDto>> ResendInvitation(Guid id, CancellationToken cancellationToken)
    {
        InvitationDto invitation = await invitationService.ResendAsync(id, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, invitation);
    }

    /// <summary>
    /// Revokes a pending invitation or deletes an expired one; either is recorded in the audit log. The caller must be
    /// able to grant its role.
    /// </summary>
    [HttpDelete("invitations/{id:guid}")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeOrDeleteInvitation(Guid id, CancellationToken cancellationToken)
    {
        await invitationService.RevokeOrDeleteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Lists the roles the caller may pick when assigning roles, without their permissions.</summary>
    [HttpGet("role-options")]
    [Authorize(Policy = Permissions.OrganizationMembersManage)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleOptionDto>>> ListRoleOptions(CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleOptionDto> options = await roleService.ListOptionsAsync(cancellationToken);

        return Ok(options);
    }

    /// <summary>Lists the roles with holder counts and the caller's edit and delete flags.</summary>
    [HttpGet("roles")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleDto> roles = await roleService.ListAsync(cancellationToken);

        return Ok(roles);
    }

    /// <summary>
    /// Creates a custom role. Every permission must be one the caller holds; the name may not match another role or a
    /// system role name in either language, ignoring case.
    /// </summary>
    [HttpPost("roles")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        RoleDto role = await roleService.CreateAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, role);
    }

    /// <summary>
    /// Renames a custom role and replaces its permissions. The caller must hold every permission the role has and
    /// every one it is given. The same name and permissions again: 200 with the role, nothing changes.
    /// </summary>
    [HttpPut("roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> UpdateRole(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        RoleDto role = await roleService.UpdateAsync(roleId, request, cancellationToken);

        return Ok(role);
    }

    /// <summary>Deletes a custom role that no active member holds. The caller must hold every permission it has.</summary>
    [HttpDelete("roles/{roleId:guid}")]
    [Authorize(Policy = Permissions.OrganizationRolesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(roleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Lists the working organization's audit entries, newest first; needs <c>auditlogs.view</c>, never an API key.
    /// A page can hold fewer than <c>limit</c> items, even none, while <c>nextCursor</c> is not <c>null</c>:
    /// pass it as <c>cursor</c> for the next page and stop only when it is <c>null</c>.
    /// </summary>
    [HttpGet("audit-log")]
    [Authorize(Policy = Permissions.AuditLogsView)]
    [ProducesResponseType(typeof(AuditEntryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditEntryPage>> GetAuditLog(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        AuditEntryPage page = await auditLogService.ListAsync(query, cancellationToken);

        return Ok(page);
    }
}
