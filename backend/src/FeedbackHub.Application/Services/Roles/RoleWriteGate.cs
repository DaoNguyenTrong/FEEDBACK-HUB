using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Application.Services.Roles;

/// <summary>The start of a manager's write on members, roles or invitations (TECH00 §5.1 step 5, §5.4).</summary>
public static class RoleWriteGate
{
    /// <summary>
    /// Lock, gate re-check, then <paramref name="permissionCode"/> from the database (the cache may still grant it
    /// after it was taken away). Call it inside the transaction, before any other read.
    /// </summary>
    /// <returns>The caller's BR-58 inputs; otherwise 403 <c>OrganizationAccessDenied</c> or <c>OrganizationSuspended</c>.</returns>
    public static async Task<RoleGrantCaller> LockAndLoadCallerAsync(
        IOrganizationLock organizationLock,
        IUnitOfWork unitOfWork,
        IRoleGrantReader roleGrantReader,
        Guid organizationId,
        Guid callerId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        await OrganizationWriteGate.LockAndRecheckAsync(
            organizationLock, unitOfWork, organizationId, callerId, cancellationToken);
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, callerId, cancellationToken);
        if (!caller.Permissions.Contains(permissionCode))
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);
        }

        return caller;
    }
}
