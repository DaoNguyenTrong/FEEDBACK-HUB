using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Services.RateLimiting;

/// <summary>
/// A fixed-window limiter whose count lives in Redis, so every API instance shares it (technical design §10.3).
/// <c>key</c> is the full Redis key, <c>feedbackhub:ratelimit:{policy}:{partition}</c>, built by the caller.
/// When Redis fails the request is counted by <c>fallback</c>, this instance's in-memory limiter: never fail open.
/// </summary>
public sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    private const string KeyPrefix = "feedbackhub:ratelimit:";

    private static readonly TimeSpan WarningInterval = TimeSpan.FromSeconds(60);

    // Last warning time (unix ms) per policy, shared by every limiter: one limiter exists per partition,
    // so a per-instance throttle would log once a minute for each partition instead of once per policy.
    private static readonly ConcurrentDictionary<string, long> LastWarningMs = new();

    // Fixed window: the first request of a window sets the TTL, so the window starts there.
    // Returns { count, pttl }; the TTL is also the Retry-After for a rejected request, at no extra round trip.
    private static readonly LuaScript Script = LuaScript.Prepare(
        """
        local count = redis.call('INCR', @key)
        if count == 1 then
          redis.call('PEXPIRE', @key, @windowMs)
        end
        return { count, redis.call('PTTL', @key) }
        """);

    private readonly IConnectionMultiplexer redis;
    private readonly string key;
    private readonly string policy;
    private readonly int permitLimit;
    private readonly long windowMs;
    private readonly RateLimiter fallback;
    private readonly ILogger logger;
    private readonly TimeProvider timeProvider;

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string key,
        int permitLimit,
        TimeSpan window,
        RateLimiter fallback,
        ILogger logger,
        TimeProvider timeProvider)
    {
        this.redis = redis;
        this.key = key;
        this.permitLimit = permitLimit;
        windowMs = (long)window.TotalMilliseconds;
        this.fallback = fallback;
        this.logger = logger;
        this.timeProvider = timeProvider;
        policy = PolicyOf(key);
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => RedisLease.Rejected(null);

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            RedisResult result = await redis.GetDatabase().ScriptEvaluateAsync(
                Script, new { key = (RedisKey)key, windowMs });
            RedisResult[] values = (RedisResult[])result!;
            long count = (long)values[0];
            long pttl = (long)values[1];

            return count <= permitLimit
                ? RedisLease.Granted
                : RedisLease.Rejected(pttl > 0 ? TimeSpan.FromMilliseconds(pttl) : null);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisConnectionException)
        {
            WarnOncePerMinute(ex);
            return await fallback.AcquireAsync(permitCount, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fallback.Dispose();
        }
    }

    private static string PolicyOf(string key)
    {
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return key;
        }

        int end = key.IndexOf(':', KeyPrefix.Length);
        return end < 0 ? key[KeyPrefix.Length..] : key[KeyPrefix.Length..end];
    }

    private void WarnOncePerMinute(Exception ex)
    {
        long now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        bool due = false;
        LastWarningMs.AddOrUpdate(
            policy,
            _ =>
            {
                due = true;
                return now;
            },
            (_, last) =>
            {
                due = now - last >= WarningInterval.TotalMilliseconds;
                return due ? now : last;
            });

        if (due)
        {
            logger.LogWarning(
                ex,
                "Redis is unavailable for the {Policy} rate limit policy; counting requests in this instance's memory instead.",
                policy);
        }
    }

    private sealed class RedisLease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public static readonly RedisLease Granted = new(true, null);

        public static RedisLease Rejected(TimeSpan? retryAfter) => new(false, retryAfter);

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is not null && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
