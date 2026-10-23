namespace FeedbackHub.Application.Common.Interfaces;

public interface ITenantAccessService
{
    /// <summary>
    /// The caller's standing in the organization. Membership is checked first: a non-member gets
    /// <see cref="TenantAccessStatus.NotMember"/> whatever the organization's state.
    /// </summary>
    Task<TenantAccessStatus> GetAccessAsync(
        Guid accountId, Guid organizationId, CancellationToken cancellationToken = default);

    Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}
