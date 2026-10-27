namespace FeedbackHub.Application.Common.Interfaces;

/// <summary>
/// The caller's standing in an organization, in the order the organization gate checks it (TECH00 §5.1 steps 2–3):
/// membership first, so a non-member never learns whether the organization is suspended.
/// </summary>
public enum TenantAccessStatus
{
    NotMember = 1,
    Suspended = 2,
    Active = 3
}
