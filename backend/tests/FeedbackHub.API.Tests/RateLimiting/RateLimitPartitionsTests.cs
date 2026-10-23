using System.Net;
using System.Threading.RateLimiting;
using FeedbackHub.Infrastructure.Services.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace FeedbackHub.API.Tests.RateLimiting;

public sealed class RateLimitPartitionsTests : IAsyncLifetime
{
    private const int PermitLimit = 3;

    private readonly RedisContainer container = new RedisBuilder("redis:8-alpine").Build();
    private ConnectionMultiplexer multiplexer = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        multiplexer = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await multiplexer.DisposeAsync();
        await container.DisposeAsync();
    }

    [Theory]
    [InlineData("Memory", "Memory")]
    [InlineData("memory", "Memory")]
    [InlineData("Redis", "Redis")]
    [InlineData("REDIS", "Redis")]
    public void Resolve_AcceptsMemoryAndRedisIgnoringCase(string value, string expected)
    {
        Assert.Equal(expected, RateLimiterStore.Resolve(value));
    }

    [Theory]
    [InlineData("Postgres")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_UnknownValue_Throws(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => RateLimiterStore.Resolve(value));
    }

    [Fact]
    public async Task FixedWindow_RedisStore_ThroughRealMiddleware_Returns429AfterLimit()
    {
        await using WebApplication app = await StartAppAsync(RateLimiterStore.Redis);
        HttpClient client = app.GetTestClient();

        for (int i = 0; i < PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/limited")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/limited")).StatusCode);

        RedisKey[] keys = multiplexer.GetServer(multiplexer.GetEndPoints()[0])
            .Keys(pattern: "feedbackhub:ratelimit:auth:*").ToArray();
        Assert.Single(keys);
        Assert.Equal("feedbackhub:ratelimit:auth:unknown", keys[0].ToString());
        Assert.Equal(PermitLimit + 1, (long)await multiplexer.GetDatabase().StringGetAsync(keys[0]));
    }

    [Fact]
    public async Task FixedWindow_MemoryStore_Returns429AfterLimit()
    {
        await using WebApplication app = await StartAppAsync(RateLimiterStore.Memory);
        HttpClient client = app.GetTestClient();

        for (int i = 0; i < PermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/limited")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/limited")).StatusCode);
        Assert.Empty(multiplexer.GetServer(multiplexer.GetEndPoints()[0]).Keys(pattern: "feedbackhub:ratelimit:*"));
    }

    [Theory]
    [InlineData(RateLimiterStore.Memory)]
    [InlineData(RateLimiterStore.Redis)]
    public void FixedWindow_NoRemoteIp_UsesUnknownPartition(string store)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };

        RateLimitPartition<string> partition =
            RateLimitPartitions.FixedWindow(context, "auth", PermitLimit, TimeSpan.FromMinutes(1), store);

        Assert.Equal("unknown", partition.PartitionKey);
    }

    [Fact]
    public void FixedWindow_PolicyNameWithColon_Throws()
    {
        DefaultHttpContext context = new();

        Assert.Throws<ArgumentException>(() => RateLimitPartitions.FixedWindow(
            context, "a:b", PermitLimit, TimeSpan.FromMinutes(1), RateLimiterStore.Memory));
    }

    private async Task<WebApplication> StartAppAsync(string store)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext => RateLimitPartitions.FixedWindow(
                httpContext, "auth", PermitLimit, TimeSpan.FromMinutes(1), store));
        });

        WebApplication app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/limited", () => Results.Ok()).RequireRateLimiting("auth");
        await app.StartAsync();
        return app;
    }
}
