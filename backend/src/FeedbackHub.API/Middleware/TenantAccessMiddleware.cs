using Microsoft.Extensions.Localization;
using FeedbackHub.API.Common;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.API.Middleware;

/// <summary>
/// The last net for endpoints that only declare <c>[Authorize]</c> but still carry an <c>org_id</c>: a non-member
/// gets <c>OrganizationAccessDenied</c>, a member of a suspended organization gets <c>OrganizationSuspended</c>
/// and the organization's name.
/// </summary>
public sealed class TenantAccessMiddleware(RequestDelegate next)
{
    private static readonly PathString[] ExemptPaths =
    [
        // Logout and switch-organization must still work once the active organization is out of reach.
        "/api/auth",
        // Self-authorizes against the organization in its URL; the caller's own list and create are org-independent.
        "/api/organizations",
        // Static, organization-independent catalogue.
        "/api/permissions",
        // Opened and accepted without an organization (TECH00 §5.1).
        "/api/invitations",
        // System-administrator scope, not organization scope.
        "/api/system"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUserService,
        ICurrentTenantProvider currentTenantProvider,
        ITenantAccessService tenantAccessService,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || ExemptPaths.Any(path => context.Request.Path.StartsWithSegments(path))
            || currentTenantProvider.OrganizationId is not { } organizationId)
        {
            await next(context);
            return;
        }

        TenantAccessStatus status = Guid.TryParse(currentUserService.UserId, out Guid accountId)
            ? await tenantAccessService.GetAccessAsync(accountId, organizationId, context.RequestAborted)
            : TenantAccessStatus.NotMember;

        if (status == TenantAccessStatus.Active)
        {
            await next(context);
            return;
        }

        // Read only after membership is confirmed, so a non-member never learns the name.
        Organization? organization = status == TenantAccessStatus.Suspended
            ? await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(organizationId, context.RequestAborted)
            : null;

        CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
            localizer,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            organization is null ? ApplicationMessages.OrganizationAccessDenied : ApplicationMessages.OrganizationSuspended);

        if (organization is not null)
        {
            problemDetails.Extensions[ProblemExtensionKeys.OrganizationName] = organization.Name;
        }

        await ApiProblemDetailsFactory.WriteAsync(context, problemDetails, context.RequestAborted);
    }
}
