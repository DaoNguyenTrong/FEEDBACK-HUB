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

    // Redis connected but not answering must not hold the request: past this the fallback counts it.
    private static readonly TimeSpan RedisWaitLimit = TimeSpan.FromMilliseconds(500);

    // Last warning time (unix ms) per policy, shared by every limiter: one limiter exists per partition,
    // so a per-instance throttle would log once a minute for each partition instead of once per policy.
    private static readonly ConcurrentDictionary<string, long> LastWarningMs = new();

    // Fixed window: the first request of a window sets the TTL, so the window starts there; a key found
    // without a TTL (a lost PEXPIRE) is given one, so it can never count forever.
    // Returns { count, pttl }; the TTL is also the Retry-After for a rejected request, at no extra round trip.
    private static readonly LuaScript Script = LuaScript.Prepare(
        """
        local count = redis.call('INCR', @key)
        if redis.call('PTTL', @key) == -1 then
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
    private long lastAcquireMs;
    private int inFlight;

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string key,
        int permitLimit,
        TimeSpan window,
        RateLimiter fallback,
        ILogger logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (fallback is ReplenishingRateLimiter { IsAutoReplenishing: false })
        {
            // This class is not a ReplenishingRateLimiter, so the partitioned limiter never replenishes the fallback.
            throw new ArgumentException("The fallback must replenish itself (AutoReplenishment = true).", nameof(fallback));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(window, TimeSpan.FromMilliseconds(1));
        policy = PolicyOf(key);

        this.redis = redis;
        this.key = key;
        this.permitLimit = permitLimit;
        windowMs = (long)window.TotalMilliseconds;
        this.fallback = fallback;
        this.logger = logger;
        this.timeProvider = timeProvider;
        lastAcquireMs = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// The partitioned limiter evicts a partition only once this exceeds its idle threshold, and treats null as
    /// "in use" forever, so a limiter that never reported idleness would pin every client key in memory.
    /// </summary>
    public override TimeSpan? IdleDuration
    {
        get
        {
            // An acquire still waiting on Redis must not look idle, or it could be evicted and disposed mid-call.
            if (Volatile.Read(ref inFlight) > 0 || fallback.IdleDuration is null)
            {
                return null;
            }

            long elapsedMs = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - Volatile.Read(ref lastAcquireMs);
            return TimeSpan.FromMilliseconds(Math.Max(0, elapsedMs));
        }
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(permitCount, 1);
        return RedisLease.Rejected(null);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(permitCount, 1);
        Volatile.Write(ref lastAcquireMs, timeProvider.GetUtcNow().ToUnixTimeMilliseconds());

        Interlocked.Increment(ref inFlight);
        try
        {
            RedisResult result = await redis.GetDatabase()
                .ScriptEvaluateAsync(Script, new { key = (RedisKey)key, windowMs })
                .WaitAsync(RedisWaitLimit, timeProvider, cancellationToken);
            RedisResult[] values = (RedisResult[])result!;
            long count = (long)values[0];
            long pttl = (long)values[1];

            return count <= permitLimit
                ? RedisLease.Granted
                : RedisLease.Rejected(pttl > 0 ? TimeSpan.FromMilliseconds(pttl) : null);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisConnectionException
                                       or RedisCommandException or TimeoutException)
        {
            WarnOncePerMinute(ex);
            return await fallback.AcquireAsync(permitCount, cancellationToken);
        }
        finally
        {
            Volatile.Write(ref lastAcquireMs, timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            Interlocked.Decrement(ref inFlight);
        }
    }

    // RateLimiter.DisposeAsync runs DisposeAsyncCore and then Dispose(false); Dispose() runs Dispose(true).
    // Each path therefore disposes the fallback exactly once.
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fallback.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore() => fallback.DisposeAsync();

    private static string PolicyOf(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The key must start with '{KeyPrefix}'.", nameof(key));
        }

        int end = key.IndexOf(':', KeyPrefix.Length);
        string name = end < 0 ? key[KeyPrefix.Length..] : key[KeyPrefix.Length..end];
        return name.Length > 0 ? name : throw new ArgumentException("The key must name a policy.", nameof(key));
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
