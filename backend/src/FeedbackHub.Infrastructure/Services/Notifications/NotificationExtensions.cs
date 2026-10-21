using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Infrastructure.Services.Jobs;
using FeedbackHub.Infrastructure.Services.Notifications;

namespace FeedbackHub.Infrastructure;

internal static class NotificationExtensions
{
    internal static IServiceCollection AddNotificationChannels(this IServiceCollection services)
    {
        services.AddScoped<IBackgroundJobDispatcher, HangfireJobDispatcher>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();

        return services;
    }
}
