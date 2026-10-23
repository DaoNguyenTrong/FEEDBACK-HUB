namespace FeedbackHub.Domain.Auditing;

/// <summary>Who performed an audited action (TECH00 §3.7).</summary>
public enum ActorKind
{
    Account = 1,
    SystemAdministrator = 2,
    System = 3
}
