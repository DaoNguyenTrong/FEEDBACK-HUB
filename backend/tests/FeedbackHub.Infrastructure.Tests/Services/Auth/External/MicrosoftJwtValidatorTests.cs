using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using FeedbackHub.Infrastructure.Services.Auth.External;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth.External;

public class MicrosoftJwtValidatorTests
{
    [Fact]
    public void BuildIssuerPattern_MultiTenantTemplate_MatchesRealTenantIssuer()
    {
        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(
            "https://login.microsoftonline.com/{tenantid}/v2.0");

        Assert.Matches(
            pattern, "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0");
    }

    [Fact]
    public void BuildIssuerPattern_MultiTenantTemplate_RejectsUnrelatedIssuer()
    {
        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(
            "https://login.microsoftonline.com/{tenantid}/v2.0");

        Assert.DoesNotMatch(pattern, "https://evil.example.com/v2.0");
    }

    [Fact]
    public void BuildIssuerPattern_FixedTenantIssuer_MatchesItself()
    {
        const string issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";

        System.Text.RegularExpressions.Regex pattern = MicrosoftJwtValidator.BuildIssuerPattern(issuer);

        Assert.Matches(pattern, issuer);
    }

    private const string ClientId = "client-1";
    private const string PersonalTenant = "9188040d-6c67-4c5b-b112-36a304b66dad";
    private const string WorkTenant = "11111111-2222-3333-4444-555555555555";

    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "k1" };

    private static MicrosoftJwtValidator CreateValidator()
    {
        OpenIdConnectConfiguration config = new() { Issuer = "https://login.microsoftonline.com/{tenantid}/v2.0" };
        config.SigningKeys.Add(SigningKey);
        return new MicrosoftJwtValidator(config);
    }

    private static string CreateToken(
        Dictionary<string, object?> claims, string? issuerTenant = null, string defaultTenant = WorkTenant)
    {
        Dictionary<string, object> payload = new()
        {
            ["sub"] = "sub-1",
            ["tid"] = defaultTenant,
            ["email"] = "user@example.com"
        };
        foreach (KeyValuePair<string, object?> pair in claims)
        {
            if (pair.Value is null)
            {
                payload.Remove(pair.Key);
            }
            else
            {
                payload[pair.Key] = pair.Value;
            }
        }

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = $"https://login.microsoftonline.com/{issuerTenant ?? defaultTenant}/v2.0",
            Audience = ClientId,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = payload,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static Task<MicrosoftTokenPayload> Validate(string token) =>
        CreateValidator().ValidateAsync(token, "common", ClientId, CancellationToken.None);

    [Fact]
    public async Task ValidateAsync_PersonalTenant_ReturnsTenantId()
    {
        MicrosoftTokenPayload payload = await Validate(
            CreateToken(new Dictionary<string, object?>(), defaultTenant: PersonalTenant));

        Assert.Equal(PersonalTenant, payload.TenantId);
        Assert.Equal("user@example.com", payload.Email);
    }

    [Theory]
    [InlineData(true)]
    [InlineData("true")]
    [InlineData("TRUE")]
    public async Task ValidateAsync_XmsEdov_BoolTrue_StringTrue_StringTRUE_AreVerified(object value)
    {
        MicrosoftTokenPayload payload = await Validate(
            CreateToken(new Dictionary<string, object?> { ["xms_edov"] = value }));

        Assert.True(payload.EmailDomainOwnerVerified);
    }

    [Theory]
    [InlineData(false)]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData(null)]
    public async Task ValidateAsync_XmsEdov_BoolFalse_OtherString_Missing_AreNotVerified(object? value)
    {
        MicrosoftTokenPayload payload = await Validate(
            CreateToken(new Dictionary<string, object?> { ["xms_edov"] = value }));

        Assert.False(payload.EmailDomainOwnerVerified);
    }

    [Fact]
    public async Task ValidateAsync_EmailMissingButPreferredUsernamePresent_EmailIsNull()
    {
        MicrosoftTokenPayload payload = await Validate(CreateToken(new Dictionary<string, object?>
        {
            ["email"] = null,
            ["preferred_username"] = "attacker@victim.com"
        }));

        Assert.Null(payload.Email);
    }

    [Fact]
    public async Task ValidateAsync_MissingTid_Throws()
    {
        string token = CreateToken(new Dictionary<string, object?> { ["tid"] = null });

        await Assert.ThrowsAnyAsync<SecurityTokenException>(() => Validate(token));
    }

    [Fact]
    public async Task ValidateAsync_IssuerTenantDiffersFromTid_Throws()
    {
        string token = CreateToken(new Dictionary<string, object?>(), issuerTenant: "99999999-9999-9999-9999-999999999999");

        await Assert.ThrowsAnyAsync<SecurityTokenException>(() => Validate(token));
    }

    [Fact]
    public async Task ValidateAsync_IssuerTenantDiffersInCaseOnly_IsAccepted()
    {
        string token = CreateToken(new Dictionary<string, object?>(), issuerTenant: WorkTenant.ToUpperInvariant());

        MicrosoftTokenPayload payload = await Validate(token);

        Assert.Equal(WorkTenant, payload.TenantId);
    }

    [Fact]
    public async Task ValidateAsync_MissingSubEvenWithOid_Throws()
    {
        string token = CreateToken(new Dictionary<string, object?> { ["sub"] = null, ["oid"] = "oid-1" });

        await Assert.ThrowsAnyAsync<SecurityTokenException>(() => Validate(token));
    }

    [Fact]
    public async Task ValidateAsync_HasOidAndSub_SubjectIsSub()
    {
        MicrosoftTokenPayload payload = await Validate(
            CreateToken(new Dictionary<string, object?> { ["oid"] = "oid-1" }));

        Assert.Equal("sub-1", payload.Subject);
    }
}
