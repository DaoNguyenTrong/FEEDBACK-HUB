using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Infrastructure.Services.Auth.External;

internal sealed class MicrosoftAuthProvider(
    IOptions<ExternalAuthSettings> externalAuthOptions,
    IMicrosoftJwtValidator microsoftJwtValidator) : IExternalAuthProvider
{
    private const string PersonalTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";

    private readonly ExternalAuthSettings externalAuthSettings = externalAuthOptions.Value;

    public string ProviderName => "Microsoft";

    public async Task<ExternalUserInfo> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        MicrosoftTokenPayload payload;

        try
        {
            payload = await microsoftJwtValidator.ValidateAsync(
                credential,
                externalAuthSettings.Microsoft.TenantId,
                externalAuthSettings.Microsoft.ClientId,
                cancellationToken);
        }
        // SecurityTokenMalformedException (non-JWT-shaped credential) derives from SecurityTokenArgumentException,
        // not SecurityTokenException; uncaught, a malicious client's malformed Credential would be a 500, not a 401.
        catch (Exception ex) when (ex is SecurityTokenException or SecurityTokenArgumentException)
        {
            throw new UnauthorizedException(ApplicationMessages.InvalidExternalCredential);
        }

        // Microsoft ID tokens carry no email_verified claim: the email counts as verified only for a personal
        // (MSA) account or when the tenant reports xms_edov = true (TECH00 6.6).
        // A missing email claim is not an error: a user already linked by sub still signs in (TECH00 6.6).
        bool emailVerified = string.Equals(payload.TenantId, PersonalTenantId, StringComparison.OrdinalIgnoreCase)
            || payload.EmailDomainOwnerVerified;

        return new ExternalUserInfo(payload.Subject, payload.Email, payload.Name, EmailVerified: emailVerified);
    }
}
