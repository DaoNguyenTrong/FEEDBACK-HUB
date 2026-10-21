using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Infrastructure.Services.Context;

namespace FeedbackHub.Infrastructure;

internal static class ContextExtensions
{
    internal static IServiceCollection AddContext(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IUserTimeZoneProvider, UserTimeZoneProvider>();

        return services;
    }
}
