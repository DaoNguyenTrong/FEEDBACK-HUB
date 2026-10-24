using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Roles;

/// <summary>A role the caller may pick when assigning roles to a member; carries no permission codes (AC-311).</summary>
public sealed record RoleOptionDto(Guid Id, string Name, SystemRoleKind? SystemKind, bool CanGrant);
