using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.SystemOrganizations;

/// <summary>
/// Tells whether an account acts on an organization as one of its members or as a system administrator from
/// outside it. Reads the membership row directly: no tenant-access cache, so the audit actor is never stale.
/// </summary>
public sealed class SystemAdministratorActor(IUnitOfWork unitOfWork)
{
    public async Task<ActorKind> ResolveAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken)
    {
        OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive,
            cancellationToken);

        return member is null ? ActorKind.SystemAdministrator : ActorKind.Account;
    }
}
