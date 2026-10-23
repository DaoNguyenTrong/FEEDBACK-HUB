using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Services.RateLimiting;

public static class RateLimitPartitions
{
    private const string UnknownPartition = "unknown";

    /// <param name="policyName">Goes into the Redis key, so it must not contain ':'.</param>
    /// <param name="store">An already resolved <see cref="RateLimiterStore"/> value.</param>
    public static RateLimitPartition<string> FixedWindow(
        HttpContext context,
        string policyName,
        int permitLimit,
        TimeSpan window,
        string store)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (policyName.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("The policy name must not contain ':'.", nameof(policyName));
        }

        string partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? UnknownPartition;

        if (store == RateLimiterStore.Memory)
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0
                });
        }

        if (store != RateLimiterStore.Redis)
        {
            throw new ArgumentException($"Unresolved rate limiter store '{store}'.", nameof(store));
        }

        IServiceProvider services = context.RequestServices;
        IConnectionMultiplexer redis = services.GetRequiredService<IConnectionMultiplexer>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<RedisFixedWindowRateLimiter>();
        TimeProvider timeProvider = services.GetRequiredService<TimeProvider>();

        return RateLimitPartition.Get(
            partitionKey,
            key => new RedisFixedWindowRateLimiter(
                redis,
                $"feedbackhub:ratelimit:{policyName}:{key}",
                permitLimit,
                window,
                // A new limiter each time (the Redis limiter disposes its fallback), and self-replenishing:
                // GetFixedWindowLimiter would force AutoReplenishment off.
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }),
                logger,
                timeProvider));
    }
}
