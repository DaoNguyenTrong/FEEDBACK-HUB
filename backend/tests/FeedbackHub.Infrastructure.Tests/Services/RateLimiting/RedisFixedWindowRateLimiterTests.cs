using System.Threading.RateLimiting;
using FeedbackHub.Infrastructure.Services.RateLimiting;
using FeedbackHub.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Tests.Services.RateLimiting;

public sealed class RedisFixedWindowRateLimiterTests(RedisContainerFixture redis) : IClassFixture<RedisContainerFixture>
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private static string NewPolicy() => $"p{Guid.NewGuid():N}";

    private static string NewKey(string policy) => $"feedbackhub:ratelimit:{policy}:203.0.113.7";

    private static RateLimiter Fallback(int limit) => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = limit,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = false,
    });

    private RedisFixedWindowRateLimiter Create(string key, int limit, TimeSpan window, ILogger? logger = null, TimeProvider? time = null) =>
        new(redis.Multiplexer, key, limit, window, Fallback(limit), logger ?? NullLogger.Instance, time ?? TimeProvider.System);

    private static IConnectionMultiplexer DeadRedis() =>
        ConnectionMultiplexer.Connect("127.0.0.1:1,abortConnect=false,connectTimeout=300,syncTimeout=300,asyncTimeout=300,connectRetry=1");

    [Fact]
    public async Task AcquireAsync_WithinLimit_Grants()
    {
        RedisFixedWindowRateLimiter limiter = Create(NewKey(NewPolicy()), 3, Window);

        for (int i = 0; i < 3; i++)
        {
            using RateLimitLease lease = await limiter.AcquireAsync(1, CancellationToken.None);
            Assert.True(lease.IsAcquired);
        }
    }

    [Fact]
    public async Task AcquireAsync_OverLimit_Rejects()
    {
        RedisFixedWindowRateLimiter limiter = Create(NewKey(NewPolicy()), 2, Window);

        (await limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        (await limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        using RateLimitLease third = await limiter.AcquireAsync(1, CancellationToken.None);

        Assert.False(third.IsAcquired);
        Assert.True(third.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter));
        Assert.InRange(retryAfter, TimeSpan.Zero, Window);
    }

    [Fact]
    public async Task AcquireAsync_TwoLimitersSameKey_ShareOneCount()
    {
        string key = NewKey(NewPolicy());
        RedisFixedWindowRateLimiter a = Create(key, 5, Window);
        RedisFixedWindowRateLimiter b = Create(key, 5, Window);

        int granted = 0;
        for (int i = 0; i < 3; i++)
        {
            using RateLimitLease la = await a.AcquireAsync(1, CancellationToken.None);
            using RateLimitLease lb = await b.AcquireAsync(1, CancellationToken.None);
            granted += (la.IsAcquired ? 1 : 0) + (lb.IsAcquired ? 1 : 0);
        }

        Assert.Equal(5, granted);
    }

    [Fact]
    public async Task AcquireAsync_AfterWindow_CountsFromZero()
    {
        RedisFixedWindowRateLimiter limiter = Create(NewKey(NewPolicy()), 1, TimeSpan.FromSeconds(1));

        (await limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        using (RateLimitLease over = await limiter.AcquireAsync(1, CancellationToken.None))
        {
            Assert.False(over.IsAcquired);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(1300));

        using RateLimitLease after = await limiter.AcquireAsync(1, CancellationToken.None);
        Assert.True(after.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_KeyAlwaysHasTtl()
    {
        string key = NewKey(NewPolicy());
        RedisFixedWindowRateLimiter limiter = Create(key, 5, Window);

        (await limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        TimeSpan? ttl = await redis.Multiplexer.GetDatabase().KeyTimeToLiveAsync(key);

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.Zero, Window);
        Assert.True(ttl.Value > TimeSpan.Zero);
    }

    [Fact]
    public async Task AcquireAsync_RedisDown_UsesFallbackAndStillLimits()
    {
        RedisFixedWindowRateLimiter limiter = new(
            DeadRedis(), NewKey(NewPolicy()), 2, Window, Fallback(2), NullLogger.Instance, TimeProvider.System);

        using RateLimitLease first = await limiter.AcquireAsync(1, CancellationToken.None);
        using RateLimitLease second = await limiter.AcquireAsync(1, CancellationToken.None);
        using RateLimitLease third = await limiter.AcquireAsync(1, CancellationToken.None);

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(third.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_RedisDown_LogsAtMostOncePerMinute()
    {
        string policy = NewPolicy();
        ManualTime time = new();
        CapturingLogger logger = new();
        IConnectionMultiplexer deadRedis = DeadRedis();
        RedisFixedWindowRateLimiter first = new(deadRedis, NewKey(policy), 100, Window, Fallback(100), logger, time);
        RedisFixedWindowRateLimiter second = new(deadRedis, $"feedbackhub:ratelimit:{policy}:198.51.100.9", 100, Window, Fallback(100), logger, time);

        for (int i = 0; i < 5; i++)
        {
            (await first.AcquireAsync(1, CancellationToken.None)).Dispose();
            (await second.AcquireAsync(1, CancellationToken.None)).Dispose();
            time.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, logger.Warnings);

        time.Advance(TimeSpan.FromSeconds(61));
        (await first.AcquireAsync(1, CancellationToken.None)).Dispose();

        Assert.Equal(2, logger.Warnings);
    }

    [Fact]
    public void AttemptAcquire_NeverCallsRedisAndIsNotAcquired()
    {
        IConnectionMultiplexer multiplexer = Substitute.For<IConnectionMultiplexer>();
        RedisFixedWindowRateLimiter limiter = new(
            multiplexer, NewKey(NewPolicy()), 5, Window, Fallback(5), NullLogger.Instance, TimeProvider.System);

        using RateLimitLease lease = limiter.AttemptAcquire(1);

        Assert.False(lease.IsAcquired);
        Assert.Empty(multiplexer.ReceivedCalls());
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class CapturingLogger : ILogger
    {
        private int warnings;

        public int Warnings => Volatile.Read(ref warnings);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Interlocked.Increment(ref warnings);
            }
        }
    }
}
