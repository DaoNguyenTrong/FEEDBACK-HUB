using FeedbackHub.Infrastructure.Services.RateLimiting;

namespace FeedbackHub.API.Tests.RateLimiting;

public sealed class RateLimiterStoreTests
{
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
}
