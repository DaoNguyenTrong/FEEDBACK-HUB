using Microsoft.EntityFrameworkCore;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Queries;

internal static class RoleDisplayOrder
{
    // SystemRoleKind is stored as text, so its alphabetical order is not the display order: rank it explicitly.
    public static IOrderedQueryable<Role> OrderForDisplay(this IQueryable<Role> roles) =>
        roles
            .OrderBy(role => role.SystemRoleKind == SystemRoleKind.Owner ? 0
                : role.SystemRoleKind == SystemRoleKind.Admin ? 1
                : role.SystemRoleKind == SystemRoleKind.Member ? 2
                : 3)
            .ThenBy(role => EF.Functions.Collate(role.Name, Collations.IcuRoot))
            .ThenBy(role => role.Id);
}
