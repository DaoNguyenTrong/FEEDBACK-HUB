using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Services.SystemAdministrators;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.Infrastructure.Tests.Services.Auth;

public class SystemAdministratorAuthorizationHandlerTests
{
    private sealed record Fixture(
        SystemAdministratorAuthorizationHandler Handler,
        ISystemAdministratorResolver Resolver,
        Guid AccountId);

    private static Fixture CreateFixture(bool isSystemAdministrator = true)
    {
        Guid accountId = Guid.NewGuid();
        HttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };

        ISystemAdministratorResolver resolver = Substitute.For<ISystemAdministratorResolver>();
        resolver.IsSystemAdministratorAsync(accountId, Arg.Any<CancellationToken>()).Returns(isSystemAdministrator);

        return new Fixture(new SystemAdministratorAuthorizationHandler(accessor, resolver), resolver, accountId);
    }

    private static ClaimsPrincipal SignedIn(string? subject, params Claim[] extra)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Name, "caller"), .. extra];
        if (subject is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, subject));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(Fixture f, ClaimsPrincipal user)
    {
        SystemAdministratorRequirement requirement = new();
        AuthorizationHandlerContext context = new([requirement], user, resource: null);

        await f.Handler.HandleAsync(context);

        return context;
    }

    private static void AssertSystemAdministratorRequired(AuthorizationHandlerContext context)
    {
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
        CodedAuthorizationFailureReason reason =
            Assert.IsType<CodedAuthorizationFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal(StatusCodes.Status403Forbidden, reason.StatusCode);
        Assert.Equal(ApplicationMessages.SystemAdministratorRequired, reason.Code);
        Assert.Null(reason.OrganizationName);
    }

    // HandleAsync

    [Fact]
    public async Task Handle_SystemAdministrator_Succeeds()
    {
        Fixture f = CreateFixture();

        AuthorizationHandlerContext context = await AuthorizeAsync(f, SignedIn(f.AccountId.ToString()));

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
        await f.Resolver.Received(1).IsSystemAdministratorAsync(f.AccountId, Arg.Any<CancellationToken>());
    }

    // AC-126: anyone else, an organization's Owner included, is refused with the system-administrator code.
    [Fact]
    public async Task Handle_NotSystemAdministrator_FailsWithSystemAdministratorRequired()
    {
        Fixture f = CreateFixture(isSystemAdministrator: false);

        AuthorizationHandlerContext context = await AuthorizeAsync(f, SignedIn(f.AccountId.ToString()));

        AssertSystemAdministratorRequired(context);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Handle_SubNotGuid_Fails(string? subject)
    {
        Fixture f = CreateFixture();

        AuthorizationHandlerContext context = await AuthorizeAsync(f, SignedIn(subject));

        AssertSystemAdministratorRequired(context);
        await f.Resolver.DidNotReceiveWithAnyArgs().IsSystemAdministratorAsync(default, default);
    }

    // The organization in the token grants nothing here: the check is the account's alone (TECH00 §5.2).
    [Fact]
    public async Task Handle_NotSystemAdministratorWithOrganizationClaim_Fails()
    {
        Fixture f = CreateFixture(isSystemAdministrator: false);
        Claim organization = new(IJwtTokenService.OrganizationIdClaimType, Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = await AuthorizeAsync(f, SignedIn(f.AccountId.ToString(), organization));

        AssertSystemAdministratorRequired(context);
    }

    [Fact]
    public async Task Handle_NotSignedIn_LeavesRequirementUnhandledForChallenge()
    {
        Fixture f = CreateFixture();

        AuthorizationHandlerContext context = await AuthorizeAsync(f, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
        await f.Resolver.DidNotReceiveWithAnyArgs().IsSystemAdministratorAsync(default, default);
    }
}
