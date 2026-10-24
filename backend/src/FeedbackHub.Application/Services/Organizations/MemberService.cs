using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Organizations;

public sealed class MemberService(
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IOrganizationMemberQueries memberQueries,
    IRoleGrantReader roleGrantReader,
    IUnitOfWork unitOfWork,
    IOrganizationLock organizationLock,
    IAuditTrail auditTrail,
    ITenantAccessService tenantAccessService,
    IPermissionResolver permissionResolver) : IMemberService
{
    public async Task ChangeRolesAsync(
        Guid accountId, UpdateMemberRolesRequest request, CancellationToken cancellationToken)
    {
        if (request.RoleIds is null || request.RoleIds.Count == 0)
        {
            throw new DomainException(ApplicationMessages.OrganizationMemberRequiresAtLeastOneRole);
        }

        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        List<Guid> requestedIds = request.RoleIds.Distinct().ToList();
        bool changed = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);
            OrganizationMember member = await LoadActiveMemberAsync(organizationId, accountId, ct);

            IReadOnlyList<RoleGrantRole> roles = await roleGrantReader.LoadRolesAsync(organizationId, requestedIds, ct);
            if (roles.Count != requestedIds.Count)
            {
                Guid missingId = requestedIds.First(id => roles.All(role => role.Id != id));
                throw new NotFoundException(nameof(Role), missingId);
            }

            IReadOnlyList<RoleGrantRole> current = await LoadRolesOfAsync(organizationId, member, ct);
            HashSet<Guid> currentIds = current.Select(role => role.Id).ToHashSet();
            List<RoleGrantRole> added = roles.Where(role => !currentIds.Contains(role.Id)).ToList();

            // For one's own roles taking away always passes, so only the roles added are checked (TECH00 §5.3).
            RoleGrantDenial? takeAway = accountId == callerId ? null : RoleGrantPolicy.CheckTakeAwayAll(caller, current);
            RoleGrantDenial? grant = RoleGrantPolicy.CheckGrantAll(caller, added);
            RoleGrantDenial? denial = grant?.Reason == RoleLockReason.OwnerOnly ? grant : takeAway ?? grant;
            if (denial is not null)
            {
                throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
            }

            if (roles.All(role => role.SystemKind != SystemRoleKind.Owner)
                && await IsLastActiveOwnerAsync(organizationId, member, current, ct))
            {
                throw new ConflictException(ApplicationMessages.OrganizationCannotRemoveLastOwner);
            }

            if (currentIds.SetEquals(roles.Select(role => role.Id)))
            {
                return;
            }

            await ReplaceRolesAsync(member, roles, ct);
            Account account = await LoadAccountAsync(accountId, ct);
            await auditTrail.RecordAsync(
                AuditEvent.For(
                    member,
                    AuditEventType.MemberRolesChanged,
                    ActorKind.Account,
                    callerId,
                    new Dictionary<string, object>
                    {
                        ["accountName"] = account.Name,
                        ["email"] = account.Email,
                        ["rolesBefore"] = ToAuditRefs(current),
                        ["rolesAfter"] = ToAuditRefs(roles)
                    }),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            changed = true;
        }, cancellationToken);

        if (changed)
        {
            await InvalidateMemberCachesAsync(organizationId, accountId, cancellationToken);
        }
    }

    public async Task RemoveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);
            OrganizationMember member = await LoadActiveMemberAsync(organizationId, accountId, ct);
            IReadOnlyList<RoleGrantRole> roles = await LoadRolesOfAsync(organizationId, member, ct);

            if (RoleGrantPolicy.CheckTakeAwayAll(caller, roles) is { } denial)
            {
                throw new ForbiddenException(RoleGrantPolicy.ErrorCode(denial));
            }

            // After BR-58, like every other 409 (D2); leaving goes only through LeaveAsync (BR-80, AC-337).
            if (accountId == callerId)
            {
                throw new ConflictException(ApplicationMessages.CannotRemoveSelf);
            }

            if (await IsLastActiveOwnerAsync(organizationId, member, roles, ct))
            {
                throw new ConflictException(ApplicationMessages.OrganizationCannotRemoveLastOwner);
            }

            Account account = await LoadAccountAsync(accountId, ct);
            await DeactivateAsync(
                member,
                AuditEventType.MemberRemoved,
                callerId,
                new Dictionary<string, object>
                {
                    ["accountName"] = account.Name,
                    ["email"] = account.Email,
                    ["roles"] = ToAuditRefs(roles)
                },
                ct);
        }, cancellationToken);

        await InvalidateMemberCachesAsync(organizationId, accountId, cancellationToken);
    }

    public async Task LeaveAsync(CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            OrganizationMember member = await LockAndRecheckGateAsync(organizationId, callerId, ct);
            IReadOnlyList<RoleGrantRole> roles = await LoadRolesOfAsync(organizationId, member, ct);

            if (await IsLastActiveOwnerAsync(organizationId, member, roles, ct))
            {
                throw new ConflictException(ApplicationMessages.LastOwnerCannotLeave);
            }

            Account account = await LoadAccountAsync(callerId, ct);
            await DeactivateAsync(
                member,
                AuditEventType.MemberLeft,
                callerId,
                new Dictionary<string, object> { ["accountName"] = account.Name, ["email"] = account.Email },
                ct);
        }, cancellationToken);

        await InvalidateMemberCachesAsync(organizationId, callerId, cancellationToken);
    }

    public async Task<PagedResult<OrganizationMemberDto>> ListAsync(
        PageRequest request, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        PagedResult<MemberRow> page = await memberQueries.ListActiveAsync(organizationId, request, cancellationToken);

        // Read from the database, never the JWT or the permission cache: it decides who sees other emails.
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(organizationId, callerId, cancellationToken);
        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await roleGrantReader.LoadRolesOfMembersAsync(
                organizationId, page.Items.Select(row => row.MemberId).ToList(), cancellationToken);

        bool canManage = caller.Permissions.Contains(Permissions.OrganizationMembersManage);

        List<OrganizationMemberDto> items = page.Items
            .Select(row => ToDto(
                row,
                rolesByMember.TryGetValue(row.MemberId, out IReadOnlyList<RoleGrantRole>? roles) ? roles : [],
                caller,
                canManage))
            .ToList();

        return new PagedResult<OrganizationMemberDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    private static OrganizationMemberDto ToDto(
        MemberRow row, IReadOnlyList<RoleGrantRole> roles, RoleGrantCaller caller, bool canManage)
    {
        bool isSelf = row.AccountId == caller.AccountId;
        List<RoleRefDto> refs = roles.Select(ToRef).ToList();
        string? email = canManage || isSelf ? row.Email : null;

        bool canChangeRoles = false;
        bool canRemove = false;
        RoleGrantDenial? denial = null;

        if (canManage && isSelf)
        {
            canChangeRoles = true;
        }
        else if (canManage)
        {
            denial = RoleGrantPolicy.CheckTakeAwayAll(caller, roles);
            canChangeRoles = denial is null;
            canRemove = denial is null;
        }

        return new OrganizationMemberDto(
            row.AccountId,
            row.AccountName,
            email,
            refs.Select(role => role.Id).ToList(),
            refs.Select(role => role.Name).ToList(),
            refs,
            isSelf,
            canChangeRoles,
            canRemove,
            denial?.Reason,
            denial?.Role is { } lockRole ? ToRef(lockRole) : null);
    }

    private static RoleRefDto ToRef(RoleGrantRole role) => new(role.Id, role.Name, role.SystemKind);

    private static List<AuditRoleRef> ToAuditRefs(IEnumerable<RoleGrantRole> roles) =>
        roles.Select(role => new AuditRoleRef(role.Id, role.Name, role.SystemKind)).ToList();

    private Task<OrganizationMember> LockAndRecheckGateAsync(
        Guid organizationId, Guid callerId, CancellationToken cancellationToken) =>
        OrganizationWriteGate.LockAndRecheckAsync(
            organizationLock, unitOfWork, organizationId, callerId, cancellationToken);

    private Task<RoleGrantCaller> LockAndLoadManagerAsync(
        Guid organizationId, Guid callerId, CancellationToken cancellationToken) =>
        RoleWriteGate.LockAndLoadCallerAsync(
            organizationLock,
            unitOfWork,
            roleGrantReader,
            organizationId,
            callerId,
            Permissions.OrganizationMembersManage,
            cancellationToken);

    private Task<OrganizationMember?> FindActiveMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken) =>
        unitOfWork.Repository<OrganizationMember, Guid>().FirstOrDefaultAsync(
            member => member.OrganizationId == organizationId && member.AccountId == accountId && member.IsActive,
            cancellationToken);

    private async Task<OrganizationMember> LoadActiveMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken) =>
        await FindActiveMemberAsync(organizationId, accountId, cancellationToken)
            ?? throw new NotFoundException(nameof(OrganizationMember), accountId);

    private async Task<IReadOnlyList<RoleGrantRole>> LoadRolesOfAsync(
        Guid organizationId, OrganizationMember member, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>> rolesByMember =
            await roleGrantReader.LoadRolesOfMembersAsync(organizationId, [member.Id], cancellationToken);

        return rolesByMember.TryGetValue(member.Id, out IReadOnlyList<RoleGrantRole>? roles) ? roles : [];
    }

    private async Task<Account> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), accountId);

    /// <returns>Whether <paramref name="member"/> holds Owner and no other active member does (BR-60).</returns>
    private async Task<bool> IsLastActiveOwnerAsync(
        Guid organizationId,
        OrganizationMember member,
        IReadOnlyList<RoleGrantRole> memberRoles,
        CancellationToken cancellationToken)
    {
        RoleGrantRole? owner = memberRoles.FirstOrDefault(role => role.SystemKind == SystemRoleKind.Owner);
        if (owner is null)
        {
            return false;
        }

        IReadOnlyList<OrganizationMemberRole> ownerLinks = await unitOfWork.Repository<OrganizationMemberRole, Guid>()
            .ListAsync(link => link.RoleId == owner.Id && link.OrganizationMemberId != member.Id, cancellationToken);
        List<Guid> otherOwnerIds = ownerLinks.Select(link => link.OrganizationMemberId).Distinct().ToList();
        if (otherOwnerIds.Count == 0)
        {
            return true;
        }

        IReadOnlyList<OrganizationMember> otherActiveOwners = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(
                other => other.OrganizationId == organizationId && other.IsActive && otherOwnerIds.Contains(other.Id),
                cancellationToken);

        return otherActiveOwners.Count == 0;
    }

    private async Task ReplaceRolesAsync(
        OrganizationMember member, IReadOnlyList<RoleGrantRole> roles, CancellationToken cancellationToken)
    {
        IRepository<OrganizationMemberRole, Guid> memberRoles = unitOfWork.Repository<OrganizationMemberRole, Guid>();
        IReadOnlyList<OrganizationMemberRole> existing =
            await memberRoles.ListAsync(link => link.OrganizationMemberId == member.Id, cancellationToken);
        HashSet<Guid> keptIds = roles.Select(role => role.Id).ToHashSet();

        foreach (OrganizationMemberRole link in existing.Where(link => !keptIds.Contains(link.RoleId)))
        {
            memberRoles.Delete(link);
        }

        HashSet<Guid> heldIds = existing.Select(link => link.RoleId).ToHashSet();
        foreach (RoleGrantRole role in roles.Where(role => !heldIds.Contains(role.Id)))
        {
            await memberRoles.AddAsync(
                OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, role.Id)), cancellationToken);
        }
    }

    // The member row stays so a later invitation reactivates it; its roles go with the membership (BR-30).
    private async Task DeactivateAsync(
        OrganizationMember member,
        AuditEventType eventType,
        Guid callerId,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        IRepository<OrganizationMemberRole, Guid> memberRoles = unitOfWork.Repository<OrganizationMemberRole, Guid>();
        foreach (OrganizationMemberRole link in
                 await memberRoles.ListAsync(link => link.OrganizationMemberId == member.Id, cancellationToken))
        {
            memberRoles.Delete(link);
        }

        member.Deactivate();
        unitOfWork.Repository<OrganizationMember, Guid>().Update(member);
        await auditTrail.RecordAsync(
            AuditEvent.For(member, eventType, ActorKind.Account, callerId, parameters), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Only after the commit: a request between the invalidation and the commit would cache the old state.
    private async Task InvalidateMemberCachesAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken)
    {
        await tenantAccessService.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
        await permissionResolver.InvalidateMemberAsync(organizationId, accountId, cancellationToken);
    }

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
