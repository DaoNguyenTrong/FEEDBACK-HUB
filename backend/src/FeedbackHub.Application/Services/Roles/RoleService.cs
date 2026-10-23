using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Roles;

public sealed class RoleService(IUnitOfWork unitOfWork) : IRoleService
{
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
