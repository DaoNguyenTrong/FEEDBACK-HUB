using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Services.Auth;

internal sealed class SystemAdministratorResolver(
    IUnitOfWork unitOfWork,
    ICacheService cacheService) : ISystemAdministratorResolver
{
    private const string Scope = "system-admin";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public Task<bool> IsSystemAdministratorAsync(Guid accountId, CancellationToken cancellationToken) =>
        cacheService.GetOrSetAsync(
            Scope,
            accountId.ToString(),
            async ct => await unitOfWork.Repository<SystemAdministrator, Guid>()
                .FirstOrDefaultAsync(a => a.AccountId == accountId, ct) is not null,
            CacheTtl,
            cancellationToken);

    public Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken) =>
        cacheService.RemoveAsync(Scope, accountId.ToString(), cancellationToken);
}
