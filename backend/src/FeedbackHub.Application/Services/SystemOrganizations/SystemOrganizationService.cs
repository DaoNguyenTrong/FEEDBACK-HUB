using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.SystemOrganizations;

public sealed class SystemOrganizationService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ISystemOrganizationQueries queries,
    SystemAdministratorActor systemAdministratorActor,
    IAuditTrail auditTrail,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    IDateTimeProvider dateTimeProvider) : ISystemOrganizationService
{
    public Task<PagedResult<SystemOrganizationDto>> ListAsync(PageRequest request, CancellationToken cancellationToken) =>
        queries.ListAsync(request, cancellationToken);

    public async Task DeactivateAsync(
        Guid organizationId, DeactivateOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new DomainException(ApplicationMessages.DeactivationReasonRequired);
        }

        Guid callerId = GetCurrentAccountId();
        bool deactivated = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
            Organization organization = await organizationRepository.GetByIdAsync(organizationId, ct)
                ?? throw new NotFoundException(nameof(Organization), organizationId);

            if (!organization.Status)
            {
                return;
            }

            ActorKind actorKind = await systemAdministratorActor.ResolveAsync(organization.Id, callerId, ct);

            // The reason stays on the organization only: the audit event never carries it (BR-84).
            organization.Deactivate(new DeactivationParams(
                callerId, actorKind == ActorKind.SystemAdministrator, request.Reason, dateTimeProvider.UtcNow));
            organizationRepository.Update(organization);
            await auditTrail.RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationDeactivated, actorKind, callerId), ct);
            await unitOfWork.SaveChangesAsync(ct);
            deactivated = true;
        }, cancellationToken);

        if (deactivated)
        {
            await InvalidateOrganizationCachesAsync(organizationId, cancellationToken);
        }
    }

    public async Task ReactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Guid callerId = GetCurrentAccountId();
        bool reactivated = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
            Organization organization = await organizationRepository.GetByIdAsync(organizationId, ct)
                ?? throw new NotFoundException(nameof(Organization), organizationId);

            if (organization.Status)
            {
                return;
            }

            ActorKind actorKind = await systemAdministratorActor.ResolveAsync(organization.Id, callerId, ct);

            organization.Reactivate();
            organizationRepository.Update(organization);
            await auditTrail.RecordAsync(
                AuditEvent.For(organization, AuditEventType.OrganizationReactivated, actorKind, callerId), ct);
            await unitOfWork.SaveChangesAsync(ct);
            reactivated = true;
        }, cancellationToken);

        if (reactivated)
        {
            await InvalidateOrganizationCachesAsync(organizationId, cancellationToken);
        }
    }

    // Only after the commit: a request between the invalidation and the commit would cache the old state again.
    private async Task InvalidateOrganizationCachesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await tenantAccessService.InvalidateOrganizationAsync(organizationId, cancellationToken);
        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
