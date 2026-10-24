using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.Infrastructure.Services.Auth;

internal sealed class PermissionResolver(
    AppDbContext dbContext,
    ICacheService cacheService,
    IOptions<PermissionResolverSettings> options) : IPermissionResolver
{
    private readonly PermissionResolverSettings settings = options.Value;

    public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return cacheService.GetOrSetAsync<IReadOnlySet<string>>(
            Scope(organizationId),
            accountId.ToString(),
            async ct =>
            {
                OrganizationMember? member = await dbContext.OrganizationMembers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, ct);

                if (member is null)
                {
                    return new HashSet<string>();
                }

                List<Role> roles = await (
                    from memberRole in dbContext.OrganizationMemberRoles.AsNoTracking()
                    join role in dbContext.Roles.AsNoTracking() on memberRole.RoleId equals role.Id
                    where memberRole.OrganizationMemberId == member.Id
                    select role)
                    .ToListAsync(ct);

                if (roles.Any(role => role.SystemRoleKind == SystemRoleKind.Owner))
                {
                    return SystemRoleDefaults.EffectivePermissionCodes(SystemRoleKind.Owner, []).ToHashSet();
                }

                List<Guid> roleIds = roles.Select(role => role.Id).ToList();

                if (roleIds.Count == 0)
                {
                    return new HashSet<string>();
                }

                List<string> codes = await dbContext.RolePermissions.AsNoTracking()
                    .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
                    .Select(rolePermission => rolePermission.PermissionCode)
                    .Distinct()
                    .ToListAsync(ct);

                return codes.ToHashSet();
            },
            TimeSpan.FromSeconds(settings.CacheTtlSeconds),
            cancellationToken);
    }

    public Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(Scope(organizationId), accountId.ToString(), cancellationToken);

    public Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        cacheService.InvalidateScopeAsync(Scope(organizationId), cancellationToken);

    private static string Scope(Guid organizationId) => $"permissions:{organizationId}";
}
