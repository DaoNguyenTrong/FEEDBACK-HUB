using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Infrastructure.Services.Security;

namespace FeedbackHub.Infrastructure;

internal static class SecurityExtensions
{
    internal static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

        return services;
    }
}
