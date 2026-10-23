namespace FeedbackHub.Application.Services.Roles;

/// <summary>
/// Reads BR-58 inputs straight from the database, never from the permission cache; inside a write transaction
/// it sees the rows locked by <c>IOrganizationLock</c> (TECH00 §5.3). Every list is in display order.
/// </summary>
public interface IRoleGrantReader
{
    /// <returns>An account that is not an active member of the organization: not Owner, no permissions.</returns>
    Task<RoleGrantCaller> LoadCallerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken);

    /// <returns>Only the roles of <paramref name="organizationId"/>; other ids are left out.</returns>
    Task<IReadOnlyList<RoleGrantRole>> LoadRolesAsync(
        Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    /// <returns>Keyed by member row id, one entry per requested id (empty list for a member with no role).</returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>>> LoadRolesOfMembersAsync(
        IReadOnlyCollection<Guid> organizationMemberIds, CancellationToken cancellationToken);
}
