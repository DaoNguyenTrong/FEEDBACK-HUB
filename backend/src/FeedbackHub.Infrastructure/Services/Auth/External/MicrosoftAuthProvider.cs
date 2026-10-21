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

        // Microsoft ID tokens carry no email_verified claim — unlike Google, Microsoft itself
        // guarantees the email on both work/school (tenant-verified) and personal (MSA-verified)
        // accounts, so there is no equivalent provider-side flag to check here.
        string email = payload.Email
            ?? throw new UnauthorizedException(ApplicationMessages.InvalidExternalCredential);

        return new ExternalUserInfo(payload.Subject, email, payload.Name, EmailVerified: true);
    }
}
