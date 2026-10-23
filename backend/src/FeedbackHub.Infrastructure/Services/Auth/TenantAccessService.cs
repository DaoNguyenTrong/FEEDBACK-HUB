using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Services.Auth;

internal sealed class TenantAccessService(
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IOptions<TenantAccessSettings> options) : ITenantAccessService
{
    private readonly TenantAccessSettings settings = options.Value;

    public Task<TenantAccessStatus> GetAccessAsync(
        Guid accountId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return cacheService.GetOrSetAsync(
            Scope(organizationId),
            Key(accountId),
            async ct =>
            {
                OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>()
                    .FirstOrDefaultAsync(
                        m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, ct);

                if (member is null)
                {
                    return TenantAccessStatus.NotMember;
                }

                Organization? organization = await unitOfWork.Repository<Organization, Guid>()
                    .GetByIdAsync(organizationId, ct);

                return organization switch
                {
                    null => TenantAccessStatus.NotMember,
                    { Status: false } => TenantAccessStatus.Suspended,
                    _ => TenantAccessStatus.Active
                };
            },
            TimeSpan.FromSeconds(settings.CacheTtlSeconds),
            cancellationToken);
    }

    public async Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
    {
        await cacheService.RemoveAsync(Scope(organizationId), Key(accountId), cancellationToken);
        // One-release migration step: instances of the previous build still read a bool under the bare account id
        // during a rolling deploy. Remove once no such build runs.
        await cacheService.RemoveAsync(Scope(organizationId), accountId.ToString(), cancellationToken);
    }

    public Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        cacheService.InvalidateScopeAsync(Scope(organizationId), cancellationToken);

    private static string Scope(Guid organizationId) => $"tenant-access:{organizationId}";

    // Not the bare account id: entries cached under it hold a bool, which would not deserialize as the status.
    private static string Key(Guid accountId) => $"{accountId}:status";
}
