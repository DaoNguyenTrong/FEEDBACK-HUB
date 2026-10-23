using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using FeedbackHub.API.Common;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;

namespace FeedbackHub.API.Authorization;

/// <summary>
/// Overrides only Forbidden (Challenge/401 and Success keep the default handler) with the coded body the exception
/// middleware writes. The first <see cref="ICodedAuthorizationFailure"/> sets status and code; a denial without one
/// is 403 <c>OrganizationAccessDenied</c>.
/// </summary>
internal sealed class AuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await Default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        IStringLocalizer<Messages> localizer =
            context.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        ICodedAuthorizationFailure? failure = authorizeResult.AuthorizationFailure?.FailureReasons
            .OfType<ICodedAuthorizationFailure>()
            .FirstOrDefault();

        int statusCode = failure?.StatusCode ?? StatusCodes.Status403Forbidden;

        CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
            localizer,
            statusCode,
            ReasonPhrases.GetReasonPhrase(statusCode),
            failure?.Code ?? ApplicationMessages.OrganizationAccessDenied);

        if (failure?.OrganizationName is { } organizationName)
        {
            problemDetails.Extensions[ProblemExtensionKeys.OrganizationName] = organizationName;
        }

        await ApiProblemDetailsFactory.WriteAsync(context, problemDetails, context.RequestAborted);
    }
}
