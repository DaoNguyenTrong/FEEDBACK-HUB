using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal sealed class RoleQueries(AppDbContext dbContext) : IRoleQueries
{
    public Task<IReadOnlyList<RoleRow>> ListAsync(
        Guid organizationId, DateTime utcNow, CancellationToken cancellationToken) =>
        LoadAsync(dbContext.Roles.Where(role => role.OrganizationId == organizationId), utcNow, cancellationToken);

    public async Task<RoleRow?> GetAsync(
        Guid organizationId, Guid roleId, DateTime utcNow, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleRow> rows = await LoadAsync(
            dbContext.Roles.Where(role => role.OrganizationId == organizationId && role.Id == roleId),
            utcNow,
            cancellationToken);

        return rows.SingleOrDefault();
    }

    // The same expression as IX_roles_OrganizationId_lower_Name, so the check and the index agree on what is equal.
    public Task<bool> IsNameTakenAsync(
        Guid organizationId, string name, Guid? excludeRoleId, CancellationToken cancellationToken) =>
        dbContext.Roles.AnyAsync(
            role => role.OrganizationId == organizationId
                && role.Id != excludeRoleId
                && EF.Functions.Collate(role.Name, Collations.IcuRoot).ToLower()
                    == EF.Functions.Collate(name, Collations.IcuRoot).ToLower(),
            cancellationToken);

    private async Task<IReadOnlyList<RoleRow>> LoadAsync(
        IQueryable<Role> roles, DateTime utcNow, CancellationToken cancellationToken)
    {
        // Holders and pending invitations are counted in the same statement: correlated COUNTs, no per-role query.
        List<StoredRole> stored = await roles
            .OrderForDisplay()
            .Select(role => new StoredRole(
                role.Id,
                role.Name,
                role.SystemRoleKind,
                dbContext.RolePermissions
                    .Where(rolePermission => rolePermission.RoleId == role.Id)
                    .Select(rolePermission => rolePermission.PermissionCode)
                    .ToList(),
                dbContext.OrganizationMemberRoles.Count(memberRole =>
                    memberRole.RoleId == role.Id
                    && dbContext.OrganizationMembers.Any(member =>
                        member.Id == memberRole.OrganizationMemberId
                        && member.OrganizationId == role.OrganizationId
                        && member.IsActive)),
                dbContext.OrganizationInvitations.Count(invitation =>
                    invitation.RoleId == role.Id
                    && invitation.OrganizationId == role.OrganizationId
                    && invitation.ExpiresAt > utcNow)))
            .ToListAsync(cancellationToken);

        return stored
            .Select(row => new RoleRow(
                row.Id,
                row.Name,
                row.SystemRoleKind,
                SystemRoleDefaults.EffectivePermissionCodes(row.SystemRoleKind, row.PermissionCodes),
                row.HolderCount,
                row.PendingInvitationCount))
            .ToList();
    }

    private sealed record StoredRole(
        Guid Id,
        string Name,
        SystemRoleKind? SystemRoleKind,
        List<string> PermissionCodes,
        int HolderCount,
        int PendingInvitationCount);
}
