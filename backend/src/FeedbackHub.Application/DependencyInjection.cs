using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Services.ApiKeys;
using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.AuditLogs;
using FeedbackHub.Application.Services.Auth;
using FeedbackHub.Application.Services.DevData;
using FeedbackHub.Application.Services.Files;
using FeedbackHub.Application.Services.Invitations;
using FeedbackHub.Application.Services.Notifications;
using FeedbackHub.Application.Services.Organizations;
using FeedbackHub.Application.Services.PermissionCatalog;
using FeedbackHub.Application.Services.Roles;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Application.Services.SystemOrganizations;
using FeedbackHub.Application.Services.SystemSettings;

namespace FeedbackHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration _)
    {
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<EmailChangeLink>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationContextService, OrganizationContextService>();
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<OrganizationStatusChanger>();
        services.AddScoped<SystemAdministratorActor>();
        services.AddScoped<ISystemOrganizationService, SystemOrganizationService>();
        services.AddScoped<ISystemAdministratorService, SystemAdministratorService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IDevDataSeeder, DevDataSeeder>();

        return services;
    }
}
