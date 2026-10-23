using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>
/// The organization gate (TECH00 §5.1, BR-77 steps 1–4), stopping at the first step not met: not signed in, outside
/// the organization, organization suspended, permission missing. Each denial carries its own code.
/// </summary>
internal sealed class OrganizationContextAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentUserService currentUserService,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    IUnitOfWork unitOfWork) : AuthorizationHandler<OrganizationContextRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationContextRequirement requirement)
    {
        // Left unhandled, so the default challenge answers 401.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        CancellationToken cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        // An API key carries no account id, so it never stands in an organization.
        if (currentTenantProvider.OrganizationId is not { } organizationId ||
            !Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            FailOutsideOrganization(context, requirement);
            return;
        }

        TenantAccessStatus status = await tenantAccessService.GetAccessAsync(accountId, organizationId, cancellationToken);

        if (status == TenantAccessStatus.Suspended)
        {
            // Read only once membership is confirmed, so a non-member never learns the name.
            Organization? organization = await unitOfWork.Repository<Organization, Guid>()
                .GetByIdAsync(organizationId, cancellationToken);

            if (organization is null)
            {
                FailOutsideOrganization(context, requirement);
                return;
            }

            context.Fail(new CodedAuthorizationFailureReason(
                this, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationSuspended, organization.Name));
            return;
        }

        if (status != TenantAccessStatus.Active)
        {
            FailOutsideOrganization(context, requirement);
            return;
        }

        if (requirement.PermissionCode is { } permissionCode)
        {
            IReadOnlySet<string> permissions = await permissionResolver.GetEffectivePermissionsAsync(
                organizationId, accountId, cancellationToken);

            if (!permissions.Contains(permissionCode))
            {
                context.Fail(new CodedAuthorizationFailureReason(
                    this, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied));
                return;
            }
        }

        context.Succeed(requirement);
    }

    private void FailOutsideOrganization(AuthorizationHandlerContext context, OrganizationContextRequirement requirement)
    {
        CodedAuthorizationFailureReason reason = requirement.DenyMode == DenyMode.NotFound
            ? new CodedAuthorizationFailureReason(
                this, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound)
            : new CodedAuthorizationFailureReason(
                this, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied);

        context.Fail(reason);
    }
}
