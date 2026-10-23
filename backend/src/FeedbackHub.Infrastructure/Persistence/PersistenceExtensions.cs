using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Application.Services.AuditTrail;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Domain.Interfaces;
using FeedbackHub.Infrastructure.Persistence;
using FeedbackHub.Infrastructure.Persistence.Queries;
using FeedbackHub.Infrastructure.Persistence.Repositories;

namespace FeedbackHub.Infrastructure;

internal static class PersistenceExtensions
{
    internal static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddDataProtection()
            .SetApplicationName("FeedbackHub")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>));
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<ISystemOrganizationQueries, SystemOrganizationQueries>();
        services.AddScoped<IPasswordAttemptStore, PasswordAttemptStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();

        return services;
    }
}
