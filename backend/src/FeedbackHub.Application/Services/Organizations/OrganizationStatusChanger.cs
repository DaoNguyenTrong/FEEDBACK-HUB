using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Organizations;

/// <summary>
/// Deactivates or reactivates an organization and records the event once, whoever asks: the row is locked for the
/// transaction, so of two simultaneous requests the second sees the committed status and changes nothing.
/// Who may act, and with which reason, is the caller's rule.
/// </summary>
public sealed class OrganizationStatusChanger(
    IUnitOfWork unitOfWork,
    IOrganizationLock organizationLock,
    IAuditTrail auditTrail,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver,
    IDateTimeProvider dateTimeProvider)
{
    /// <param name="resolveActorKind">Runs inside the transaction, only when the organization is still active.</param>
    /// <exception cref="NotFoundException">No such organization.</exception>
    public Task DeactivateAsync(
        Guid organizationId,
        Guid actorAccountId,
        string? reason,
        Func<Organization, CancellationToken, Task<ActorKind>> resolveActorKind,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            organizationId,
            actorAccountId,
            targetStatus: false,
            AuditEventType.OrganizationDeactivated,
            resolveActorKind,
            // The reason stays on the organization only: the audit event never carries it (BR-84).
            (organization, actorKind) => organization.Deactivate(new DeactivationParams(
                actorAccountId, actorKind == ActorKind.SystemAdministrator, reason, dateTimeProvider.UtcNow)),
            cancellationToken);

    /// <param name="resolveActorKind">Runs inside the transaction, only when the organization is deactivated.</param>
    /// <exception cref="NotFoundException">No such organization.</exception>
    public Task ReactivateAsync(
        Guid organizationId,
        Guid actorAccountId,
        Func<Organization, CancellationToken, Task<ActorKind>> resolveActorKind,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            organizationId,
            actorAccountId,
            targetStatus: true,
            AuditEventType.OrganizationReactivated,
            resolveActorKind,
            (organization, _) => organization.Reactivate(),
            cancellationToken);

    private async Task ChangeStatusAsync(
        Guid organizationId,
        Guid actorAccountId,
        bool targetStatus,
        AuditEventType eventType,
        Func<Organization, CancellationToken, Task<ActorKind>> resolveActorKind,
        Action<Organization, ActorKind> apply,
        CancellationToken cancellationToken)
    {
        bool changed = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await organizationLock.AcquireAsync(organizationId, ct);
            IRepository<Organization, Guid> organizationRepository = unitOfWork.Repository<Organization, Guid>();
            Organization organization = await organizationRepository.GetByIdAsync(organizationId, ct)
                ?? throw new NotFoundException(nameof(Organization), organizationId);

            if (organization.Status == targetStatus)
            {
                return;
            }

            ActorKind actorKind = await resolveActorKind(organization, ct);

            apply(organization, actorKind);
            organizationRepository.Update(organization);
            await auditTrail.RecordAsync(AuditEvent.For(organization, eventType, actorKind, actorAccountId), ct);
            await unitOfWork.SaveChangesAsync(ct);
            changed = true;
        }, cancellationToken);

        if (changed)
        {
            // Only after the commit: a request between the invalidation and the commit would cache the old state.
            await tenantAccessService.InvalidateOrganizationAsync(organizationId, cancellationToken);
            await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
        }
    }
}
