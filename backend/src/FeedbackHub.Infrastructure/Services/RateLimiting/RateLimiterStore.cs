using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure.Services.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FeedbackHub.Infrastructure.Services.RateLimiting;

/// <summary>Where the rate-limit counters live: this instance's memory, or Redis shared by every instance.</summary>
public static class RateLimiterStore
{
    public const string Memory = "Memory";
    public const string Redis = "Redis";

    /// <summary>Normalises <c>RateLimiterSettings:Store</c>, case-insensitively; any other value fails startup.</summary>
    public static string Resolve(string? value)
    {
        if (string.Equals(value, Memory, StringComparison.OrdinalIgnoreCase))
        {
            return Memory;
        }

        if (string.Equals(value, Redis, StringComparison.OrdinalIgnoreCase))
        {
            return Redis;
        }

        throw new InvalidOperationException(
            $"Unknown RateLimiterSettings:Store '{value}'. Supported values: {Memory}, {Redis}.");
    }

    internal static IServiceCollection AddRateLimiterStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        RateLimiterSettings settings = configuration
            .GetSection(nameof(RateLimiterSettings))
            .Get<RateLimiterSettings>() ?? new RateLimiterSettings();

        services.TryAddSingleton(TimeProvider.System);

        if (Resolve(settings.Store) == Redis)
        {
            RedisConnection.RequireConnectionString(
                configuration, $"{nameof(RateLimiterSettings)}:{nameof(RateLimiterSettings.Store)}=Redis");
            CachingExtensions.EnsureRedisMultiplexer(services, configuration);
        }

        return services;
    }
}
