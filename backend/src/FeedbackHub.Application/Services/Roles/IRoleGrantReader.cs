namespace FeedbackHub.Application.Services.Roles;

/// <summary>
/// Reads BR-58 inputs straight from the database, never from the permission cache; inside a write transaction
/// it sees the rows locked by <c>IOrganizationLock</c> (TECH00 §5.3). Every list is in display order.
/// </summary>
public interface IRoleGrantReader
{
    /// <returns>An account that is not an active member of the organization: not Owner, no permissions.</returns>
    Task<RoleGrantCaller> LoadCallerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken);

    /// <returns>
    /// Each role of <paramref name="organizationId"/> once (duplicate ids merged); ids outside it are left out. Callers
    /// answer 404 when <c>result.Count != roleIds.Distinct().Count()</c> and write only the loaded role ids.
    /// </returns>
    Task<IReadOnlyList<RoleGrantRole>> LoadRolesAsync(
        Guid organizationId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    /// <returns>
    /// Keyed by member row id: an entry (empty when it has no role) for each requested member of
    /// <paramref name="organizationId"/>, none for an id outside it; roles of other organizations are left out.
    /// </returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<RoleGrantRole>>> LoadRolesOfMembersAsync(
        Guid organizationId, IReadOnlyCollection<Guid> organizationMemberIds, CancellationToken cancellationToken);
}
