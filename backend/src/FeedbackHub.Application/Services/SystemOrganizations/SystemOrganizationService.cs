using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.SystemOrganizations;

public sealed class SystemOrganizationService(
    ICurrentUserService currentUserService,
    ISystemOrganizationQueries queries,
    SystemAdministratorActor systemAdministratorActor,
    OrganizationStatusChanger organizationStatusChanger) : ISystemOrganizationService
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

        await organizationStatusChanger.DeactivateAsync(
            organizationId,
            callerId,
            request.Reason,
            // Authorized by the SystemAdministrator policy, not by a permission of the organization.
            recheckCaller: null,
            (organization, ct) => systemAdministratorActor.ResolveAsync(organization.Id, callerId, ct),
            cancellationToken);
    }

    public async Task ReactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        Guid callerId = GetCurrentAccountId();

        await organizationStatusChanger.ReactivateAsync(
            organizationId,
            callerId,
            (organization, ct) => systemAdministratorActor.ResolveAsync(organization.Id, callerId, ct),
            cancellationToken);
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
