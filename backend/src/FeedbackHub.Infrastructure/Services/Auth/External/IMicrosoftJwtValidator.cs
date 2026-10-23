using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FeedbackHub.Infrastructure.Services.Auth.External;

internal sealed record MicrosoftTokenPayload(
    string Subject, string? Email, string? Name, string TenantId, bool EmailDomainOwnerVerified);

internal interface IMicrosoftJwtValidator
{
    Task<MicrosoftTokenPayload> ValidateAsync(
        string credential, string tenantId, string clientId, CancellationToken cancellationToken);
}

// For common/organizations/consumers tenants the discovery document's Issuer is the literal template
// "https://login.microsoftonline.com/{tenantid}/v2.0" — no fixed issuer exists, so the placeholder becomes
// a regex instead of TokenValidationParameters.ValidIssuer.
internal sealed class MicrosoftJwtValidator : IMicrosoftJwtValidator
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers = new();

    private readonly OpenIdConnectConfiguration? configurationOverride;

    public MicrosoftJwtValidator()
    {
    }

    // Test seam: supplies the OIDC configuration (signing keys, issuer template) instead of fetching discovery.
    internal MicrosoftJwtValidator(OpenIdConnectConfiguration configuration)
    {
        configurationOverride = configuration;
    }

    public async Task<MicrosoftTokenPayload> ValidateAsync(
        string credential, string tenantId, string clientId, CancellationToken cancellationToken)
    {
        OpenIdConnectConfiguration config;
        if (configurationOverride is not null)
        {
            config = configurationOverride;
        }
        else
        {
            ConfigurationManager<OpenIdConnectConfiguration> configManager = ConfigManagers.GetOrAdd(tenantId, id =>
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"https://login.microsoftonline.com/{id}/v2.0/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever()));

            config = await configManager.GetConfigurationAsync(cancellationToken);
        }

        Regex issuerPattern = BuildIssuerPattern(config.Issuer);

        TokenValidationParameters validationParameters = new()
        {
            ValidateIssuer = true,
            IssuerValidator = (issuer, _, _) => issuerPattern.IsMatch(issuer)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not valid."),
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // MapInboundClaims = false: claims keep their raw names (sub, email, tid, xms_edov); the default
        // mapping renames them and silently falls back to the wrong claims (TECH00 6.6).
        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        ClaimsPrincipal principal = handler.ValidateToken(credential, validationParameters, out SecurityToken validatedToken);

        string subject = principal.FindFirst("sub")?.Value
            ?? throw new SecurityTokenException("Token is missing a subject claim.");
        string tid = principal.FindFirst("tid")?.Value
            ?? throw new SecurityTokenException("Token is missing a tenant claim.");

        string? issuerTenant = ExtractIssuerTenant(validatedToken.Issuer);
        if (issuerTenant is null || !string.Equals(issuerTenant, tid, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidIssuerException("Issuer tenant does not match the tid claim.");
        }

        string? email = principal.FindFirst("email")?.Value;
        string? name = principal.FindFirst("name")?.Value;
        // Read the raw payload value: a JSON array (even ["true"]) is split into per-element claims and would
        // otherwise look like a plain "true". Only a JSON boolean true or the string "true" counts.
        bool emailDomainOwnerVerified = validatedToken is JwtSecurityToken jwt
            && jwt.Payload.TryGetValue("xms_edov", out object? edov)
            && edov switch
            {
                bool b => b,
                string str => string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };

        return new MicrosoftTokenPayload(subject, email, name, tid, emailDomainOwnerVerified);
    }

    // Expected shape: https://login.microsoftonline.com/{tenant}/v2.0
    private static string? ExtractIssuerTenant(string? issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && segments[1] == "v2.0" ? segments[0] : null;
    }

    // Regex.Escape escapes only the opening brace ("\{tenantid}"), so escaping first and then replacing the
    // placeholder silently never matches — failing every login. Split on the raw placeholder before escaping.
    internal static Regex BuildIssuerPattern(string issuerTemplate)
    {
        string[] segments = issuerTemplate.Split("{tenantid}");
        return new Regex("^" + string.Join("[^/]+", segments.Select(Regex.Escape)) + "$");
    }
}
