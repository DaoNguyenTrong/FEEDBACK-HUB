namespace FeedbackHub.Application.Common.Settings;

public sealed class RateLimiterSettings
{
    // Memory counts per instance; Redis counts across all instances (technical design §10.3).
    public string Store { get; init; } = "Memory";

    public int AuthPermitLimit { get; init; } = 5;

    public int AuthWindowMinutes { get; init; } = 1;

    // Refresh is called by every open tab on a schedule and behind shared NAT/proxy
    // IPs, so it needs a much looser bucket than the login/register endpoints —
    // tripping it logs the user out (the client's failed-refresh path clears auth).
    public int RefreshPermitLimit { get; init; } = 60;

    public int RefreshWindowMinutes { get; init; } = 1;
}
