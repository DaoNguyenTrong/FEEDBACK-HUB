namespace FeedbackHub.Domain.Entities;

/// <summary>Which confirmation a pending email change is waiting for.</summary>
public enum EmailChangeStage
{
    /// <summary>Password-less account: the link sent to the current address has not been confirmed yet.</summary>
    AwaitingCurrentEmail,

    /// <summary>The link sent to the new address is the next step.</summary>
    AwaitingNewEmail
}
