using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using FeedbackHub.API.Authorization;
using FeedbackHub.Application.Resources;
using FeedbackHub.Infrastructure.Services.Auth;

namespace FeedbackHub.API.Tests.Authorization;

public class AuthorizationResultHandlerTests
{
    private const string OrganizationName = "Suspended Co";

    private sealed record Fixture(AuthorizationResultHandler Handler, DefaultHttpContext Context, IAuthorizationHandler Source);

    private static Fixture CreateFixture()
    {
        IStringLocalizer<Messages> localizer = Substitute.For<IStringLocalizer<Messages>>();
        localizer[Arg.Any<string>()]
            .Returns(callInfo => new LocalizedString(callInfo.ArgAt<string>(0), $"localized:{callInfo.ArgAt<string>(0)}"));

        ServiceCollection services = new();
        services.AddSingleton(localizer);

        DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        return new Fixture(new AuthorizationResultHandler(), context, Substitute.For<IAuthorizationHandler>());
    }

    private static async Task<JsonElement> ForbidAndReadBodyAsync(Fixture f, params AuthorizationFailureReason[] reasons)
    {
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        PolicyAuthorizationResult result = PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed(reasons));

        await f.Handler.HandleAsync(_ => Task.CompletedTask, f.Context, policy, result);

        f.Context.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument document = await JsonDocument.ParseAsync(f.Context.Response.Body);
        return document.RootElement.Clone();
    }

    // HandleAsync

    [Fact]
    public async Task HandleAsync_NotFoundReason_Writes404ResourceNotFound()
    {
        Fixture f = CreateFixture();

        JsonElement body = await ForbidAndReadBodyAsync(
            f, new CodedAuthorizationFailureReason(f.Source, StatusCodes.Status404NotFound, ApplicationMessages.ResourceNotFound));

        Assert.Equal(StatusCodes.Status404NotFound, f.Context.Response.StatusCode);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal(ApplicationMessages.ResourceNotFound, body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("organizationName", out _));
    }

    // AC-301: the name sits at the top level, beside the code, as the middleware writes it.
    [Fact]
    public async Task HandleAsync_SuspendedReason_Writes403WithOrganizationName()
    {
        Fixture f = CreateFixture();

        JsonElement body = await ForbidAndReadBodyAsync(
            f,
            new CodedAuthorizationFailureReason(
                f.Source, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationSuspended, OrganizationName));

        Assert.Equal(StatusCodes.Status403Forbidden, f.Context.Response.StatusCode);
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.GetProperty("code").GetString());
        Assert.Equal(OrganizationName, body.GetProperty("organizationName").GetString());
        Assert.False(body.TryGetProperty("extensions", out _));
    }

    [Fact]
    public async Task HandleAsync_NoCodedReason_Writes403AccessDenied()
    {
        Fixture f = CreateFixture();

        JsonElement body = await ForbidAndReadBodyAsync(f, new AuthorizationFailureReason(f.Source, "uncoded"));

        Assert.Equal(StatusCodes.Status403Forbidden, f.Context.Response.StatusCode);
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("organizationName", out _));
    }

    [Fact]
    public async Task HandleAsync_SeveralCodedReasons_WritesTheFirst()
    {
        Fixture f = CreateFixture();

        JsonElement body = await ForbidAndReadBodyAsync(
            f,
            new AuthorizationFailureReason(f.Source, "uncoded"),
            new CodedAuthorizationFailureReason(
                f.Source, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationSuspended, OrganizationName),
            new CodedAuthorizationFailureReason(
                f.Source, StatusCodes.Status403Forbidden, ApplicationMessages.OrganizationAccessDenied));

        Assert.Equal(ApplicationMessages.OrganizationSuspended, body.GetProperty("code").GetString());
    }
}
