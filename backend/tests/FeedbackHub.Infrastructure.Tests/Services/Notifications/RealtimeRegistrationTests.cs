using System.Net;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FeedbackHub.Infrastructure.Tests.Services.Notifications;

public sealed class RealtimeRegistrationTests
{
    [Fact]
    public async Task AddRealtime_RedisBackplane_DoesNotAbortWhenRedisIsDownAtStart()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RealtimeSettings:Backplane"] = "Redis",
                ["ConnectionStrings:Redis"] = "redis:6379"
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddRealtime(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();

        RedisOptions options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;

        Assert.False(options.Configuration.AbortOnConnectFail);
        Assert.Equal(RedisChannel.Literal("FeedbackHub"), options.Configuration.ChannelPrefix);
        DnsEndPoint endPoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.Configuration.EndPoints));
        Assert.Equal(("redis", 6379), (endPoint.Host, endPoint.Port));
    }
}
