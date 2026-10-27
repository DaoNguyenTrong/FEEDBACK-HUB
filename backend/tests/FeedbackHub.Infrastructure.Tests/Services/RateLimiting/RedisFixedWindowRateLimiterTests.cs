using System.Diagnostics;
using System.Threading.RateLimiting;
using FeedbackHub.Infrastructure;
using FeedbackHub.Infrastructure.Services.RateLimiting;
using FeedbackHub.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Tests.Services.RateLimiting;

public sealed class RedisFixedWindowRateLimiterTests(RedisContainerFixture redis) : IClassFixture<RedisContainerFixture>
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private sealed record Fixture(
        RedisFixedWindowRateLimiter Limiter,
        CountingFallback Fallback,
        ManualTime Time,
        CapturingLogger Logger,
        string Key,
        ConnectionMultiplexer? OwnedRedis) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Limiter.DisposeAsync();
            if (OwnedRedis is not null)
            {
                await OwnedRedis.DisposeAsync();
            }
        }
    }

    private Fixture CreateFixture(
        string? policy = null,
        string partition = "203.0.113.7",
        int limit = 5,
        TimeSpan? window = null,
        bool redisDown = false,
        IConnectionMultiplexer? multiplexer = null,
        string? key = null,
        ManualTime? time = null,
        CapturingLogger? logger = null)
    {
        key ??= $"feedbackhub:ratelimit:{policy ?? $"p{Guid.NewGuid():N}"}:{partition}";
        // The production options (not test-tuned timeouts), so an outage test measures what a request really waits.
        ConnectionMultiplexer? owned = redisDown
            ? ConnectionMultiplexer.Connect(CachingExtensions.CreateRedisOptions("127.0.0.1:1"))
            : null;
        CountingFallback fallback = new(Math.Max(1, limit));
        time ??= new ManualTime();
        logger ??= new CapturingLogger();
        try
        {
            RedisFixedWindowRateLimiter limiter = new(
                multiplexer ?? owned ?? redis.Multiplexer, key, limit, window ?? Window, fallback, logger, time);
            return new Fixture(limiter, fallback, time, logger, key, owned);
        }
        catch
        {
            fallback.Dispose();
            owned?.Dispose();
            throw;
        }
    }

    // AcquireAsync

    [Fact]
    public async Task AcquireAsync_WithinLimit_Grants()
    {
        await using Fixture f = CreateFixture(limit: 3);

        for (int i = 0; i < 3; i++)
        {
            using RateLimitLease lease = await f.Limiter.AcquireAsync(1, CancellationToken.None);
            Assert.True(lease.IsAcquired);
        }
    }

    [Fact]
    public async Task AcquireAsync_OverLimit_RejectsWithRetryAfter()
    {
        await using Fixture f = CreateFixture(limit: 2);

        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        using RateLimitLease third = await f.Limiter.AcquireAsync(1, CancellationToken.None);

        Assert.False(third.IsAcquired);
        Assert.True(third.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter));
        Assert.InRange(retryAfter, TimeSpan.Zero, Window);
    }

    [Fact]
    public async Task AcquireAsync_TwoLimitersSameKey_ShareOneCount()
    {
        await using Fixture a = CreateFixture(limit: 5);
        await using Fixture b = CreateFixture(limit: 5, key: a.Key);

        int granted = 0;
        for (int i = 0; i < 3; i++)
        {
            using RateLimitLease la = await a.Limiter.AcquireAsync(1, CancellationToken.None);
            using RateLimitLease lb = await b.Limiter.AcquireAsync(1, CancellationToken.None);
            granted += (la.IsAcquired ? 1 : 0) + (lb.IsAcquired ? 1 : 0);
        }

        Assert.Equal(5, granted);
    }

    [Fact]
    public async Task AcquireAsync_AfterWindow_CountsFromZero()
    {
        await using Fixture f = CreateFixture(limit: 1, window: TimeSpan.FromSeconds(1));

        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        using (RateLimitLease over = await f.Limiter.AcquireAsync(1, CancellationToken.None))
        {
            Assert.False(over.IsAcquired);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(1300));

        using RateLimitLease after = await f.Limiter.AcquireAsync(1, CancellationToken.None);
        Assert.True(after.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_KeyAlwaysHasTtl()
    {
        await using Fixture f = CreateFixture();

        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
        TimeSpan? ttl = await redis.Multiplexer.GetDatabase().KeyTimeToLiveAsync(f.Key);

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMilliseconds(1), Window);
    }

    [Fact]
    public async Task AcquireAsync_KeyWithoutTtl_GetsOne()
    {
        await using Fixture f = CreateFixture();
        await redis.Multiplexer.GetDatabase().StringSetAsync(f.Key, 1);

        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();

        TimeSpan? ttl = await redis.Multiplexer.GetDatabase().KeyTimeToLiveAsync(f.Key);
        Assert.NotNull(ttl);
    }

    [Fact]
    public async Task AcquireAsync_RedisDown_UsesFallbackAndStillLimits()
    {
        await using Fixture f = CreateFixture(limit: 2, redisDown: true);

        using RateLimitLease first = await f.Limiter.AcquireAsync(1, CancellationToken.None);
        using RateLimitLease second = await f.Limiter.AcquireAsync(1, CancellationToken.None);
        using RateLimitLease third = await f.Limiter.AcquireAsync(1, CancellationToken.None);

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(third.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_RedisDown_FallbackAnswersWellUnderOneSecond()
    {
        await using Fixture f = CreateFixture(limit: 100, redisDown: true);

        for (int i = 0; i < 3; i++)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            using RateLimitLease lease = await f.Limiter.AcquireAsync(1, CancellationToken.None);
            elapsed.Stop();

            Assert.True(lease.IsAcquired);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), $"Acquire {i} took {elapsed.Elapsed}.");
        }

        Assert.Equal(3, f.Fallback.AcquireCount);
    }

    [Fact]
    public async Task AcquireAsync_RedisConnectedButScriptNeverReturns_FallsBackWithinTheBound()
    {
        (IConnectionMultiplexer pending, _) = PendingRedis();
        await using Fixture f = CreateFixture(multiplexer: pending);
        Stopwatch elapsed = Stopwatch.StartNew();

        // The outer guard only keeps a regression from hanging the run; the bound under test is 500 ms.
        using RateLimitLease lease = await f.Limiter.AcquireAsync(1, CancellationToken.None).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));
        elapsed.Stop();

        Assert.True(lease.IsAcquired);
        Assert.Equal(1, f.Fallback.AcquireCount);
        Assert.InRange(elapsed.Elapsed, TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AcquireAsync_RedisServerError_UsesFallback()
    {
        await using Fixture f = CreateFixture(limit: 1);
        await redis.Multiplexer.GetDatabase().HashSetAsync(f.Key, "field", "value"); // INCR on a hash: WRONGTYPE

        using RateLimitLease first = await f.Limiter.AcquireAsync(1, CancellationToken.None);
        using RateLimitLease second = await f.Limiter.AcquireAsync(1, CancellationToken.None);

        Assert.True(first.IsAcquired);
        Assert.False(second.IsAcquired);
        Assert.Equal(2, f.Fallback.AcquireCount);
    }

    [Fact]
    public async Task AcquireAsync_CancelledWhileScriptPending_Propagates()
    {
        (IConnectionMultiplexer pending, _) = PendingRedis();
        await using Fixture f = CreateFixture(multiplexer: pending, time: new ManualTime { TimersNeverFire = true });
        using CancellationTokenSource cts = new();

        Task<RateLimitLease> acquire = f.Limiter.AcquireAsync(1, cts.Token).AsTask();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await acquire);
        Assert.Equal(0, f.Fallback.AcquireCount);
    }

    [Fact]
    public async Task AcquireAsync_RedisDown_LogsAtMostOncePerMinute()
    {
        string policy = $"p{Guid.NewGuid():N}";
        ManualTime time = new();
        CapturingLogger logger = new();
        await using Fixture first = CreateFixture(policy, limit: 100, redisDown: true, time: time, logger: logger);
        await using Fixture second = CreateFixture(policy, "198.51.100.9", limit: 100, redisDown: true, time: time, logger: logger);

        for (int i = 0; i < 5; i++)
        {
            (await first.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
            (await second.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();
            time.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, logger.Warnings);

        time.Advance(TimeSpan.FromSeconds(61));
        (await first.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();

        Assert.Equal(2, logger.Warnings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AcquireAsync_PermitCountNotOne_Throws(int permitCount)
    {
        await using Fixture f = CreateFixture();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await f.Limiter.AcquireAsync(permitCount, CancellationToken.None));
    }

    // AttemptAcquire

    [Fact]
    public async Task AttemptAcquire_Always_ReturnsNotAcquiredWithoutRedis()
    {
        IConnectionMultiplexer multiplexer = Substitute.For<IConnectionMultiplexer>();
        await using Fixture f = CreateFixture(multiplexer: multiplexer);

        using RateLimitLease lease = f.Limiter.AttemptAcquire(1);

        Assert.False(lease.IsAcquired);
        Assert.Empty(multiplexer.ReceivedCalls());
    }

    // IdleDuration

    [Fact]
    public async Task IdleDuration_FallbackIdle_CountsFromTheLastAcquire()
    {
        await using Fixture f = CreateFixture();
        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();

        f.Time.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(20), f.Limiter.IdleDuration);
    }

    [Fact]
    public async Task IdleDuration_FallbackInUse_IsNull()
    {
        await using Fixture f = CreateFixture();
        f.Fallback.InUse = true;
        f.Time.Advance(TimeSpan.FromSeconds(20));

        Assert.Null(f.Limiter.IdleDuration);
    }

    [Fact]
    public async Task IdleDuration_AcquireInFlight_IsNullUntilItEnds()
    {
        (IConnectionMultiplexer pending, _) = PendingRedis();
        await using Fixture f = CreateFixture(multiplexer: pending, time: new ManualTime { TimersNeverFire = true });
        using CancellationTokenSource cts = new();

        Task<RateLimitLease> acquire = f.Limiter.AcquireAsync(1, cts.Token).AsTask();
        f.Time.Advance(TimeSpan.FromSeconds(20));

        Assert.Null(f.Limiter.IdleDuration);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await acquire);

        Assert.Equal(TimeSpan.Zero, f.Limiter.IdleDuration);
    }

    [Fact]
    public async Task IdleDuration_AfterAnotherAcquire_RestartsFromZero()
    {
        await using Fixture f = CreateFixture();
        f.Time.Advance(TimeSpan.FromSeconds(20));

        (await f.Limiter.AcquireAsync(1, CancellationToken.None)).Dispose();

        Assert.Equal(TimeSpan.Zero, f.Limiter.IdleDuration);
    }

    // Dispose

    [Fact]
    public async Task Dispose_Sync_DisposesTheFallbackOnce()
    {
        await using Fixture f = CreateFixture();

        f.Limiter.Dispose();

        Assert.Equal(1, f.Fallback.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await f.Fallback.AcquireAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task DisposeAsync_Always_DisposesTheFallbackOnce()
    {
        await using Fixture f = CreateFixture();

        await f.Limiter.DisposeAsync();

        Assert.Equal(1, f.Fallback.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await f.Fallback.AcquireAsync(1, CancellationToken.None));
    }

    // Constructor

    [Fact]
    public async Task Constructor_FallbackNotAutoReplenishing_Throws()
    {
        await using FixedWindowRateLimiter manual = new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = Window,
            AutoReplenishment = false,
        });

        Assert.Throws<ArgumentException>(() => new RedisFixedWindowRateLimiter(
            redis.Multiplexer, "feedbackhub:ratelimit:p:1.2.3.4", 5, Window, manual, new CapturingLogger(), new ManualTime()));
    }

    [Fact]
    public void Constructor_PermitLimitBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateFixture(limit: 0));

    [Fact]
    public void Constructor_WindowBelowOneMillisecond_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateFixture(window: TimeSpan.FromTicks(1)));

    [Theory]
    [InlineData("other:prefix:x")]
    [InlineData("feedbackhub:ratelimit:")]
    [InlineData("feedbackhub:ratelimit::203.0.113.7")]
    public void Constructor_InvalidKey_Throws(string key) =>
        Assert.Throws<ArgumentException>(() => CreateFixture(key: key));

    /// <summary>A Redis whose script call never completes: the acquire stays in flight until its token is cancelled.</summary>
    private static (IConnectionMultiplexer Multiplexer, TaskCompletionSource<RedisResult> Script) PendingRedis()
    {
        TaskCompletionSource<RedisResult> script = new();
        IDatabase database = Substitute.For<IDatabase>();
        database.ScriptEvaluateAsync(Arg.Any<LuaScript>(), Arg.Any<object?>(), Arg.Any<CommandFlags>()).Returns(script.Task);
        IConnectionMultiplexer multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(database);
        return (multiplexer, script);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;

        /// <summary>
        /// True: timers never fire, so a test controls the limiter's WaitAsync bound itself (cancels instead of
        /// racing 500 ms). False: real timers, for the test that needs the bound to fire.
        /// </summary>
        public bool TimersNeverFire { get; init; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            return TimersNeverFire ? new InertTimer() : base.CreateTimer(callback, state, dueTime, period);
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
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

    /// <summary>The production-shaped fallback (auto-replenishing fixed window) with counters on top.</summary>
    private sealed class CountingFallback : RateLimiter
    {
        private readonly FixedWindowRateLimiter inner;
        private int acquireCount;
        private int disposeCount;

        public CountingFallback(int limit) => inner = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

        public bool InUse { get; set; }

        public int AcquireCount => Volatile.Read(ref acquireCount);

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public override TimeSpan? IdleDuration => InUse ? null : TimeSpan.Zero;

        public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();

        protected override RateLimitLease AttemptAcquireCore(int permitCount) => inner.AttemptAcquire(permitCount);

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref acquireCount);
            return inner.AcquireAsync(permitCount, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref disposeCount);
                inner.Dispose();
            }
        }

        protected override ValueTask DisposeAsyncCore()
        {
            Interlocked.Increment(ref disposeCount);
            return inner.DisposeAsync();
        }
    }
}
