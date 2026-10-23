using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal sealed class RoleGrantReader(AppDbContext dbContext) : IRoleGrantReader
{
    public async Task<RoleGrantCaller> LoadCallerAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken)
    {
        Guid? memberId = await dbContext.OrganizationMembers
            .Where(member => member.OrganizationId == organizationId && member.AccountId == accountId && member.IsActive)
            .Select(member => (Guid?)member.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberId is null)
        {
            return new RoleGrantCaller(accountId, false, new HashSet<string>());
        }

        Guid organizationMemberId = memberId.Value;

        IReadOnlyList<RoleGrantRole> roles = await LoadOrderedAsync(
            dbContext.Roles.Where(role =>
                role.OrganizationId == organizationId
                && dbContext.OrganizationMemberRoles.Any(memberRole =>
                    memberRole.OrganizationMemberId == organizationMemberId && memberRole.RoleId == role.Id)),
            cancellationToken);

        return new RoleGrantCaller(
            accountId,
            roles.Any(role => role.SystemKind == SystemRoleKind.Owner),
            roles.SelectMany(role => role.Permissions).ToHashSet());
    }

    public async Task<IReadOnlyList<RoleGrantRole>> LoadRolesAsync(
        Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = roleIds.ToArray();

        return await LoadOrderedAsync(
            dbContext.Roles.Where(role => role.OrganizationId == organizationId && ids.Contains(role.Id)),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>>> LoadRolesOfMembersAsync(
        IReadOnlyCollection<Guid> organizationMemberIds, CancellationToken cancellationToken)
    {
        if (organizationMemberIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<RoleGrantRole>>();
        }

        Guid[] memberIds = organizationMemberIds.Distinct().ToArray();

        List<MemberRoleLink> links = await dbContext.OrganizationMemberRoles
            .Where(memberRole => memberIds.Contains(memberRole.OrganizationMemberId))
            .Select(memberRole => new MemberRoleLink(memberRole.OrganizationMemberId, memberRole.RoleId))
            .ToListAsync(cancellationToken);

        Guid[] roleIds = links.Select(link => link.RoleId).Distinct().ToArray();
        IReadOnlyList<RoleGrantRole> roles = await LoadOrderedAsync(
            dbContext.Roles.Where(role => roleIds.Contains(role.Id)), cancellationToken);

        // Filtering the SQL-ordered list keeps the display order; nothing is re-sorted in memory.
        ILookup<Guid, Guid> roleIdsByMember = links.ToLookup(link => link.OrganizationMemberId, link => link.RoleId);

        return memberIds.ToDictionary(
            memberId => memberId,
            memberId =>
            {
                HashSet<Guid> held = roleIdsByMember[memberId].ToHashSet();
                return (IReadOnlyList<RoleGrantRole>)roles.Where(role => held.Contains(role.Id)).ToList();
            });
    }

    private async Task<IReadOnlyList<RoleGrantRole>> LoadOrderedAsync(
        IQueryable<Role> roles, CancellationToken cancellationToken)
    {
        List<RoleRow> rows = await roles
            .OrderForDisplay()
            .Select(role => new RoleRow(
                role.Id,
                role.Name,
                role.SystemRoleKind,
                dbContext.RolePermissions
                    .Where(rolePermission => rolePermission.RoleId == role.Id)
                    .Select(rolePermission => rolePermission.PermissionCode)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new RoleGrantRole(
                row.Id,
                row.Name,
                row.SystemRoleKind,
                SystemRoleDefaults.EffectivePermissionCodes(row.SystemRoleKind, row.PermissionCodes).ToHashSet()))
            .ToList();
    }

    private sealed record MemberRoleLink(Guid OrganizationMemberId, Guid RoleId);

    private sealed record RoleRow(Guid Id, string Name, SystemRoleKind? SystemRoleKind, List<string> PermissionCodes);
}
