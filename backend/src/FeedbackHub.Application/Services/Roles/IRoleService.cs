using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

public interface IRoleService
{
    /// <summary>
    /// Stages (does not save) the 3 system roles for a newly created organization, returning them
    /// keyed by kind so the caller can link the creating member to the seeded Owner role in the
    /// same unit of work.
    /// </summary>
    Task<IReadOnlyDictionary<SystemRoleKind, Role>> SeedSystemRolesAsync(
        Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Roles the caller may assign, in display order, each with whether BR-58 lets the caller grant it.</summary>
    Task<IReadOnlyList<RoleOptionDto>> ListOptionsAsync(CancellationToken cancellationToken);

    /// <summary>The Roles section: every role with holder count and the caller's edit and delete flags.</summary>
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken);
}
