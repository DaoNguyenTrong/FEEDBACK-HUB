using Microsoft.AspNetCore.Authorization;

namespace FeedbackHub.Infrastructure.Services.Auth;

/// <summary>
/// The signed-in account is a system administrator. Checked by <see cref="SystemAdministratorAuthorizationHandler"/>,
/// whatever organization the token names.
/// </summary>
public sealed class SystemAdministratorRequirement : IAuthorizationRequirement;
