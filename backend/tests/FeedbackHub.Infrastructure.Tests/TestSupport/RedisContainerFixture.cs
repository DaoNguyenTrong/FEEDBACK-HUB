using StackExchange.Redis;
using Testcontainers.Redis;

namespace FeedbackHub.Infrastructure.Tests.TestSupport;

/// <summary>One real Redis container per test class, shared by every test in it.</summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer container = new RedisBuilder("redis:8-alpine").Build();

    public ConnectionMultiplexer Multiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        Multiplexer = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Multiplexer.DisposeAsync();
        await container.DisposeAsync();
    }
}
