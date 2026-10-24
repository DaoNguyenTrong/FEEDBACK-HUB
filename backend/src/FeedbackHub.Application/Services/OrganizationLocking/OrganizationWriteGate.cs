using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.OrganizationLocking;

/// <summary>The start of every write that changes members, roles or invitations of an organization.</summary>
public static class OrganizationWriteGate
{
    /// <summary>
    /// Takes the organization lock first (TECH00 §5.4), then answers as the gate would now: the caller may have been
    /// removed, or the organization suspended, after the gate let the request in. Call inside the transaction.
    /// </summary>
    /// <returns>The caller's active member row; otherwise 403 <c>OrganizationAccessDenied</c> or <c>OrganizationSuspended</c>.</returns>
    public static async Task<OrganizationMember> LockAndRecheckAsync(
        IOrganizationLock organizationLock,
        IUnitOfWork unitOfWork,
        Guid organizationId,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        await organizationLock.AcquireAsync(organizationId, cancellationToken);

        OrganizationMember callerMember = await unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
                member => member.OrganizationId == organizationId && member.AccountId == callerId && member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);

        Organization organization =
            await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(organizationId, cancellationToken)
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        if (!organization.Status)
        {
            throw new ForbiddenException(
                ApplicationMessages.OrganizationSuspended,
                new Dictionary<string, string> { [ProblemExtensionKeys.OrganizationName] = organization.Name });
        }

        return callerMember;
    }
}
