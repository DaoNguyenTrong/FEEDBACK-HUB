using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Organizations;

public sealed class OrganizationContextService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IOrganizationLock organizationLock,
    IRoleGrantReader roleGrantReader,
    ISystemAdministratorResolver systemAdministratorResolver,
    IAuditTrail auditTrail,
    OrganizationStatusChanger organizationStatusChanger) : IOrganizationContextService
{
    public async Task<CurrentOrganizationDto> GetAsync(CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Organization organization = await LoadAsync(organizationId, cancellationToken);

        return ToDto(organization);
    }

    public async Task<CurrentOrganizationDto> RenameAsync(
        RenameOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException(DomainMessages.OrganizationNameRequired);
        }

        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        string name = request.Name.Trim();
        Organization? renamed = null;

        // Locked, so two renames at once each record the name the other one left.
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Removed or deactivated after the gate let the request in: answer as the gate now would.
            await OrganizationWriteGate.LockAndRecheckAsync(organizationLock, unitOfWork, organizationId, callerId, ct);
            await EnsureCallerManagesAsync(organizationId, callerId, ct);
            Organization organization = await LoadAsync(organizationId, ct);
            renamed = organization;

            if (string.Equals(organization.Name, name, StringComparison.Ordinal))
            {
                return;
            }

            string nameBefore = organization.Name;
            organization.Update(new OrganizationNameParams(name));
            unitOfWork.Repository<Organization, Guid>().Update(organization);
            await auditTrail.RecordAsync(
                AuditEvent.For(
                    organization,
                    AuditEventType.OrganizationRenamed,
                    ActorKind.Account,
                    callerId,
                    new Dictionary<string, object> { ["nameBefore"] = nameBefore, ["nameAfter"] = organization.Name }),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        // Set by the operation above, which either ran to the end or threw.
        return ToDto(renamed!);
    }

    public async Task DeactivateAsync(DeactivateOrganizationRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        // BR-54: a system administrator must say why, even when acting as a member of the organization.
        if (string.IsNullOrWhiteSpace(request.Reason)
            && await systemAdministratorResolver.IsSystemAdministratorAsync(callerId, cancellationToken))
        {
            throw new DomainException(ApplicationMessages.DeactivationReasonRequired);
        }

        // The gate let only an active member through, so the actor is always the member's account (AC-369).
        await organizationStatusChanger.DeactivateAsync(
            organizationId,
            callerId,
            request.Reason,
            ct => EnsureCallerManagesAsync(organizationId, callerId, ct),
            (_, _) => Task.FromResult(ActorKind.Account),
            cancellationToken);
    }

    /// <summary>
    /// The endpoint's permission <c>organizations.manage</c> read from the database after the lock: the permission
    /// cache may still grant it after it was taken away, or while the caller waited for the lock.
    /// </summary>
    private async Task EnsureCallerManagesAsync(Guid organizationId, Guid callerId, CancellationToken cancellationToken)
    {
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, callerId, cancellationToken);
        if (!caller.Permissions.Contains(Permissions.OrganizationManage))
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }
    }

    private async Task<Organization> LoadAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), organizationId);

    private static CurrentOrganizationDto ToDto(Organization organization) =>
        new(organization.Id, organization.Name, organization.Slug, organization.Status);

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
