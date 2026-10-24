using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.OrganizationLocking;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Roles;

public sealed class RoleService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IRoleQueries roleQueries,
    IRoleGrantReader roleGrantReader,
    IOrganizationLock organizationLock,
    IAuditTrail auditTrail,
    IPermissionResolver permissionResolver) : IRoleService
{
    public async Task<IReadOnlyList<RoleOptionDto>> ListOptionsAsync(CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(
            organizationId, GetCurrentAccountId(), cancellationToken);
        IReadOnlyList<RoleRow> rows = await roleQueries.ListAsync(organizationId, cancellationToken);

        return rows
            .Select(row => new RoleOptionDto(
                row.Id, row.Name, row.SystemKind, RoleGrantPolicy.CanGrant(caller, ToGrantRole(row))))
            .ToList();
    }

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        RoleGrantCaller caller = await roleGrantReader.LoadCallerAsync(
            organizationId, GetCurrentAccountId(), cancellationToken);
        IReadOnlyList<RoleRow> rows = await roleQueries.ListAsync(organizationId, cancellationToken);

        return rows.Select(row => ToDto(row, caller)).ToList();
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> codes = ValidatePermissionCodes(request.PermissionCodes);
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        RoleDto? created = null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, ct);
            EnsureCallerHolds(caller, codes);

            Role role = Role.Create(new RoleParams(organizationId, request.Name));
            await EnsureNameAvailableAsync(organizationId, role.Name, null, ct);

            await unitOfWork.Repository<Role, Guid>().AddAsync(role, ct);
            IRepository<RolePermission, Guid> rolePermissions = unitOfWork.Repository<RolePermission, Guid>();
            foreach (string code in codes)
            {
                await rolePermissions.AddAsync(RolePermission.Create(new RolePermissionParams(role.Id, code)), ct);
            }

            await auditTrail.RecordAsync(
                AuditEvent.For(
                    role,
                    AuditEventType.RoleCreated,
                    ActorKind.Account,
                    callerId,
                    new Dictionary<string, object>
                    {
                        ["role"] = AuditRoleRef.From(role),
                        ["permissionCodes"] = codes
                    }),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            created = await LoadDtoAsync(organizationId, role.Id, caller, ct);
        }, cancellationToken);

        return created ?? throw new InvalidOperationException("The role transaction completed without a result.");
    }

    public async Task<RoleDto> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> codes = ValidatePermissionCodes(request.PermissionCodes);
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();
        RoleDto? result = null;
        bool changed = false;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (Role role, RoleGrantCaller caller, RoleGrantRole current) =
                await LockAndLoadEditableRoleAsync(organizationId, callerId, roleId, ct);
            EnsureCallerHolds(caller, codes);

            // Role.Update normalizes the name; the entity is detached, so nothing reaches the database before Update().
            string nameBefore = role.Name;
            role.Update(new RoleParams(organizationId, request.Name));
            await EnsureNameAvailableAsync(organizationId, role.Name, role.Id, ct);

            List<string> added = codes.Where(code => !current.Permissions.Contains(code)).ToList();
            List<string> removed = InCatalogOrder(current.Permissions.Where(code => !codes.Contains(code)));
            bool renamed = role.Name != nameBefore;

            if (renamed || added.Count > 0 || removed.Count > 0)
            {
                await ApplyChangesAsync(role, renamed, added, removed, ct);
                await auditTrail.RecordAsync(
                    AuditEvent.For(
                        role,
                        AuditEventType.RoleUpdated,
                        ActorKind.Account,
                        callerId,
                        new Dictionary<string, object>
                        {
                            ["nameBefore"] = nameBefore,
                            ["nameAfter"] = role.Name,
                            ["permissionsAdded"] = added,
                            ["permissionsRemoved"] = removed
                        }),
                    ct);
                await unitOfWork.SaveChangesAsync(ct);
                changed = true;
            }

            result = await LoadDtoAsync(organizationId, role.Id, caller, ct);
        }, cancellationToken);

        if (changed)
        {
            await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
        }

        return result ?? throw new InvalidOperationException("The role transaction completed without a result.");
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        Guid organizationId = RequireOrganizationId();
        Guid callerId = GetCurrentAccountId();

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            (Role role, _, _) = await LockAndLoadEditableRoleAsync(organizationId, callerId, roleId, ct);

            RoleRow row = await roleQueries.GetAsync(organizationId, role.Id, ct)
                ?? throw new NotFoundException(nameof(Role), roleId);
            if (row.HolderCount > 0)
            {
                throw new ConflictException(ApplicationMessages.RoleHasHolders);
            }

            // Recorded before the delete so the entry is built from the role while it still exists.
            await auditTrail.RecordAsync(
                AuditEvent.For(
                    role,
                    AuditEventType.RoleDeleted,
                    ActorKind.Account,
                    callerId,
                    new Dictionary<string, object> { ["role"] = AuditRoleRef.From(role) }),
                ct);
            unitOfWork.Repository<Role, Guid>().Delete(role);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await permissionResolver.InvalidateOrganizationAsync(organizationId, cancellationToken);
    }

    /// <summary>
    /// The prelude of API-17 and API-18, in the D2 order: lock, gate re-check and roles.manage, the role in the
    /// organization (404), not a system role (403 <c>OrganizationSystemRoleImmutable</c>), then BR-58 CanTakeAway.
    /// </summary>
    private async Task<(Role Role, RoleGrantCaller Caller, RoleGrantRole Current)> LockAndLoadEditableRoleAsync(
        Guid organizationId, Guid callerId, Guid roleId, CancellationToken cancellationToken)
    {
        RoleGrantCaller caller = await LockAndLoadManagerAsync(organizationId, callerId, cancellationToken);

        Role role = await unitOfWork.Repository<Role, Guid>().FirstOrDefaultAsync(
                candidate => candidate.Id == roleId && candidate.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);
        if (role.SystemRoleKind is not null)
        {
            throw new ForbiddenException(ApplicationMessages.OrganizationSystemRoleImmutable);
        }

        IReadOnlyList<RoleGrantRole> loaded =
            await roleGrantReader.LoadRolesAsync(organizationId, [role.Id], cancellationToken);
        RoleGrantRole current = loaded.Count == 1 ? loaded[0] : throw new NotFoundException(nameof(Role), roleId);
        if (!RoleGrantPolicy.CanTakeAway(caller, current))
        {
            throw new ForbiddenException(ApplicationMessages.InsufficientRolePermissions);
        }

        return (role, caller, current);
    }

    private Task<RoleGrantCaller> LockAndLoadManagerAsync(
        Guid organizationId, Guid callerId, CancellationToken cancellationToken) =>
        RoleWriteGate.LockAndLoadCallerAsync(
            organizationLock,
            unitOfWork,
            roleGrantReader,
            organizationId,
            callerId,
            Permissions.OrganizationRolesManage,
            cancellationToken);

    private async Task ApplyChangesAsync(
        Role role, bool renamed, IReadOnlyList<string> added, IReadOnlyList<string> removed, CancellationToken ct)
    {
        if (renamed)
        {
            unitOfWork.Repository<Role, Guid>().Update(role);
        }

        IRepository<RolePermission, Guid> rolePermissions = unitOfWork.Repository<RolePermission, Guid>();
        if (removed.Count > 0)
        {
            IReadOnlyList<RolePermission> links = await rolePermissions.ListAsync(
                link => link.RoleId == role.Id && removed.Contains(link.PermissionCode), ct);
            foreach (RolePermission link in links)
            {
                rolePermissions.Delete(link);
            }
        }

        foreach (string code in added)
        {
            await rolePermissions.AddAsync(RolePermission.Create(new RolePermissionParams(role.Id, code)), ct);
        }
    }

    // BR-79: the name is checked after Role.Update has trimmed and NFC-normalized it.
    private async Task EnsureNameAvailableAsync(
        Guid organizationId, string name, Guid? excludeRoleId, CancellationToken cancellationToken)
    {
        if (SystemRoleDisplayNames.IsReserved(name))
        {
            throw new ConflictException(ApplicationMessages.RoleNameReservedForSystemRole);
        }

        if (await roleQueries.IsNameTakenAsync(organizationId, name, excludeRoleId, cancellationToken))
        {
            throw new ConflictException(ApplicationMessages.OrganizationRoleNameAlreadyExists);
        }
    }

    // BR-58 point 2: a custom role carries only permissions the caller holds.
    private static void EnsureCallerHolds(RoleGrantCaller caller, IReadOnlyList<string> codes)
    {
        if (!RoleGrantPolicy.HasAllPermissions(caller, codes))
        {
            throw new ForbiddenException(ApplicationMessages.InsufficientRolePermissions);
        }
    }

    /// <returns>The codes once each, in catalog order.</returns>
    /// <exception cref="DomainException"><c>UnknownPermissionCode</c> for a code outside the catalog.</exception>
    private static IReadOnlyList<string> ValidatePermissionCodes(IReadOnlyList<string> permissionCodes)
    {
        if (permissionCodes.Any(code => !Permissions.All.Contains(code)))
        {
            throw new DomainException(ApplicationMessages.UnknownPermissionCode);
        }

        return InCatalogOrder(permissionCodes);
    }

    private static List<string> InCatalogOrder(IEnumerable<string> codes)
    {
        HashSet<string> wanted = codes.ToHashSet();
        return Permissions.All.Where(wanted.Contains)
            .Concat(wanted.Where(code => !Permissions.All.Contains(code)))
            .ToList();
    }

    private async Task<RoleDto> LoadDtoAsync(
        Guid organizationId, Guid roleId, RoleGrantCaller caller, CancellationToken cancellationToken)
    {
        RoleRow row = await roleQueries.GetAsync(organizationId, roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);
        return ToDto(row, caller);
    }

    /// <summary>The one place a <see cref="RoleDto"/> is built, so every role carries the same flags.</summary>
    internal static RoleDto ToDto(RoleRow row, RoleGrantCaller caller)
    {
        bool isSystem = row.SystemKind is not null;
        bool canTakeAway = RoleGrantPolicy.CanTakeAway(caller, ToGrantRole(row));

        RoleDeleteBlockedReason? blocked = isSystem ? RoleDeleteBlockedReason.SystemRole
            : !canTakeAway ? RoleDeleteBlockedReason.InsufficientRolePermissions
            : row.HolderCount > 0 ? RoleDeleteBlockedReason.HasHolders
            : null;

        return new RoleDto(
            row.Id,
            row.Name,
            row.SystemKind,
            isSystem,
            row.PermissionCodes,
            row.HolderCount,
            !isSystem && canTakeAway,
            blocked is null,
            blocked);
    }

    private static RoleGrantRole ToGrantRole(RoleRow row) =>
        new(row.Id, row.Name, row.SystemKind, row.PermissionCodes.ToHashSet());

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);

    private Guid GetCurrentAccountId() =>
        Guid.TryParse(currentUserService.UserId, out Guid accountId)
            ? accountId
            : throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);

    public async Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        IRepository<Role, Guid> roleRepository = unitOfWork.Repository<Role, Guid>();
        Dictionary<SystemRoleKind, Role> roles = [];

        foreach (SystemRoleKind kind in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organizationId, kind.ToString(), kind));
            await roleRepository.AddAsync(role, cancellationToken);
            roles[kind] = role;

            if (SystemRoleDefaults.PermissionsByKind.TryGetValue(kind, out IReadOnlyList<string>? codes))
            {
                await AddRolePermissionsAsync(role.Id, codes, cancellationToken);
            }
        }

        return roles;
    }

    private async Task AddRolePermissionsAsync(
        Guid roleId, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken)
    {
        IRepository<RolePermission, Guid> rolePermissionRepository = unitOfWork.Repository<RolePermission, Guid>();

        foreach (string code in permissionCodes.Distinct())
        {
            await rolePermissionRepository.AddAsync(
                RolePermission.Create(new RolePermissionParams(roleId, code)), cancellationToken);
        }
    }
}
