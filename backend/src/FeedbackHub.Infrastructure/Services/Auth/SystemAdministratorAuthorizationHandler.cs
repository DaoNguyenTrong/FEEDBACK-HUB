using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.SystemAdministrators;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>
/// Policy <c>SystemAdministrator</c> (TECH00 §5.2): the account in <c>sub</c> is a system administrator. Never reads
/// <c>org_id</c>, so the organization a session works in neither grants nor blocks this area.
/// </summary>
internal sealed class SystemAdministratorAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ISystemAdministratorResolver systemAdministratorResolver) : AuthorizationHandler<SystemAdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SystemAdministratorRequirement requirement)
    {
        // Left unhandled, so the default challenge answers 401.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        CancellationToken cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid accountId) &&
            await systemAdministratorResolver.IsSystemAdministratorAsync(accountId, cancellationToken))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new CodedAuthorizationFailureReason(
            this, StatusCodes.Status403Forbidden, ApplicationMessages.SystemAdministratorRequired));
    }
}
