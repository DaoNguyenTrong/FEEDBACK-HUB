namespace FeedbackHub.Application.Services.Roles;

public enum RoleDeleteBlockedReason
{
    SystemRole = 1,
    InsufficientRolePermissions = 2,
    HasHolders = 3
}
