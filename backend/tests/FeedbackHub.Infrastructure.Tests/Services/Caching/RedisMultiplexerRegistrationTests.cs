using FeedbackHub.Infrastructure.Services.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Tests.Services.Caching;

public sealed class RedisMultiplexerRegistrationTests
{
    [Fact]
    public async Task EnsureRedisMultiplexer_UnreachableRedisWithoutAbortConnect_ReturnsMultiplexerInsteadOfThrowing()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "127.0.0.1:1,connectTimeout=500,syncTimeout=500"
            })
            .Build();
        ServiceCollection services = new();
        CachingExtensions.EnsureRedisMultiplexer(services, configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();

        IConnectionMultiplexer multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();

        Assert.False(multiplexer.IsConnected);
    }

    [Fact]
    public void CreateRedisOptions_Always_FailsFastInsteadOfQueueingDuringAnOutage()
    {
        ConfigurationOptions options = CachingExtensions.CreateRedisOptions("redis:6379");

        Assert.False(options.AbortOnConnectFail);
        Assert.Same(BacklogPolicy.FailFast, options.BacklogPolicy);
    }

    [Fact]
    public void CreateRedisOptions_ConnectionStringSaysAbortConnect_StillDoesNotAbort()
    {
        ConfigurationOptions options = CachingExtensions.CreateRedisOptions("redis:6379,abortConnect=true");

        Assert.False(options.AbortOnConnectFail);
    }
}
