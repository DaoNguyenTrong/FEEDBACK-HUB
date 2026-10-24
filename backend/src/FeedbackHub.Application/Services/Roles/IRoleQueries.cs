namespace FeedbackHub.Application.Services.Roles;

public interface IRoleQueries
{
    /// <returns>Every role of the organization in display order: Owner, Admin, Member, then custom roles.</returns>
    Task<IReadOnlyList<RoleRow>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <returns>The role, or <c>null</c> when it does not exist in <paramref name="organizationId"/>.</returns>
    Task<RoleRow?> GetAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether another role of the organization has <paramref name="name"/>, ignoring case the way the unique index
    /// on <c>lower("Name" COLLATE "und-x-icu")</c> does. <paramref name="name"/> is already trimmed and NFC.
    /// </summary>
    Task<bool> IsNameTakenAsync(
        Guid organizationId, string name, Guid? excludeRoleId, CancellationToken cancellationToken);
}
