using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Services.Auth.External;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth.External;

public class MicrosoftAuthProviderTests
{
    private sealed record Fixture(MicrosoftAuthProvider Provider, IMicrosoftJwtValidator Validator);

    private static Fixture CreateFixture(string clientId = "test-client-id", string tenantId = "common")
    {
        IOptions<ExternalAuthSettings> options = Options.Create(new ExternalAuthSettings
        {
            Microsoft = new MicrosoftAuthSettings { ClientId = clientId, TenantId = tenantId }
        });
        IMicrosoftJwtValidator validator = Substitute.For<IMicrosoftJwtValidator>();

        MicrosoftAuthProvider provider = new(options, validator);

        return new Fixture(provider, validator);
    }

    [Fact]
    public void ProviderName_IsMicrosoft()
    {
        Fixture f = CreateFixture();

        Assert.Equal("Microsoft", f.Provider.ProviderName);
    }

    [Fact]
    public async Task ValidateAsync_ValidPayload_MapsToExternalUserInfo()
    {
        Fixture f = CreateFixture();
        MicrosoftTokenPayload payload = new("microsoft-sub-1", "user@example.com", "Test User", "9188040d-6c67-4c5b-b112-36a304b66dad", false);
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(payload);

        ExternalUserInfo result = await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        Assert.Equal("microsoft-sub-1", result.ProviderUserId);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Test User", result.Name);
        Assert.True(result.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_PassesConfiguredTenantAndClientId()
    {
        Fixture f = CreateFixture(clientId: "configured-client-id", tenantId: "configured-tenant-id");
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("sub", "e@example.com", null, "t", false));

        await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        await f.Validator.Received(1).ValidateAsync(
            "some-credential", "configured-tenant-id", "configured-client-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_SecurityTokenException_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<MicrosoftTokenPayload>(_ => throw new SecurityTokenInvalidIssuerException("bad issuer"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("bad-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_SecurityTokenMalformedException_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<MicrosoftTokenPayload>(_ => throw new SecurityTokenMalformedException("not a JWT"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("garbage-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_NoEmailClaim_ReturnsNullEmailWithoutThrowing()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("sub", null, "Test User", "t", false));

        ExternalUserInfo result = await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        Assert.Null(result.Email);
        Assert.Equal("sub", result.ProviderUserId);
    }

    [Fact]
    public async Task ValidateAsync_PersonalTenant_EmailVerifiedTrue()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("s", "e@example.com", null, "9188040D-6C67-4C5B-B112-36A304B66DAD", false));

        ExternalUserInfo result = await f.Provider.ValidateAsync("c", CancellationToken.None);

        Assert.True(result.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_WorkTenantWithoutXmsEdov_EmailVerifiedFalse()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("s", "e@example.com", null, "11111111-1111-1111-1111-111111111111", false));

        ExternalUserInfo result = await f.Provider.ValidateAsync("c", CancellationToken.None);

        Assert.False(result.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_WorkTenantWithXmsEdov_EmailVerifiedTrue()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("s", "e@example.com", null, "11111111-1111-1111-1111-111111111111", true));

        ExternalUserInfo result = await f.Provider.ValidateAsync("c", CancellationToken.None);

        Assert.True(result.EmailVerified);
    }
}
