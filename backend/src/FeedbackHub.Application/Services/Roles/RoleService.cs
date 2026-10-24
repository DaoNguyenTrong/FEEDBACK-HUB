using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Roles;

public sealed class RoleService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICurrentTenantProvider currentTenantProvider,
    IRoleQueries roleQueries,
    IRoleGrantReader roleGrantReader) : IRoleService
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
