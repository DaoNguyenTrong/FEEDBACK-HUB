using System.Text;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <summary>
/// Display names of the three system roles per UI language, and the names a custom role may not take (BR-79).
/// The same texts live in the frontend locale <c>roles.systemKind</c>; a test keeps the two in step.
/// </summary>
public static class SystemRoleDisplayNames
{
    private static readonly HashSet<string> Reserved = ["owner", "admin", "member", "chủ sở hữu"];

    public static string Get(SystemRoleKind kind, string language)
    {
        bool vietnamese = language == "vi";
        return kind switch
        {
            SystemRoleKind.Owner => vietnamese ? "Chủ sở hữu" : "Owner",
            SystemRoleKind.Admin => "Admin",
            SystemRoleKind.Member => "Member",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static bool IsReserved(string name) =>
        Reserved.Contains(name.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant());
}
