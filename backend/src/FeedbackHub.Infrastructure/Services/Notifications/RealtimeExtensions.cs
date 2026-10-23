using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Services.Notifications;
using FeedbackHub.Infrastructure.Services.Caching;
using FeedbackHub.Infrastructure.Services.Notifications;

namespace FeedbackHub.Infrastructure;

internal static class RealtimeExtensions
{
    internal const string NoneBackplane = "None";
    internal const string RedisBackplane = "Redis";

    internal static IServiceCollection AddRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RealtimeSettings>(configuration.GetSection(nameof(RealtimeSettings)));

        RealtimeSettings realtimeSettings = configuration
            .GetSection(nameof(RealtimeSettings))
            .Get<RealtimeSettings>() ?? new RealtimeSettings();

        string backplane = ResolveBackplane(realtimeSettings.Backplane);

        if (backplane == RedisBackplane)
        {
            string connectionString = RedisConnection.RequireConnectionString(
                configuration, $"{nameof(RealtimeSettings)}:{nameof(RealtimeSettings.Backplane)}=Redis");

            CachingExtensions.EnsureRedisMultiplexer(services, configuration);

            // Its own connection: the hub lifetime manager disposes the multiplexer it is given, and pub/sub keeps
            // the default backlog so a short blip delays messages instead of dropping them.
            services.AddSignalR().AddStackExchangeRedis(connectionString, options =>
            {
                options.Configuration.AbortOnConnectFail = false;
                options.Configuration.ChannelPrefix = RedisChannel.Literal("FeedbackHub");
            });
        }
        else
        {
            services.AddSignalR();
        }

        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }

    internal static string ResolveBackplane(string backplane)
    {
        if (string.Equals(backplane, NoneBackplane, StringComparison.OrdinalIgnoreCase))
        {
            return NoneBackplane;
        }

        if (string.Equals(backplane, RedisBackplane, StringComparison.OrdinalIgnoreCase))
        {
            return RedisBackplane;
        }

        throw new InvalidOperationException(
            $"Unknown RealtimeSettings:Backplane '{backplane}'. Supported values: {NoneBackplane}, {RedisBackplane}.");
    }
}
