using FeedbackHub.API.OpenApi;
using FeedbackHub.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace FeedbackHub.API.Tests.OpenApi;

public sealed class AuthorizationResponsesOperationTransformerTests
{
    private const string DocumentName = "v1";

    private readonly AuthorizationResponsesOperationTransformer _transformer = new();

    // Transform
    [Fact]
    public async Task Transform_AnonymousAction_AddsNothing()
    {
        OpenApiOperation operation = new();

        await Run(operation, new AllowAnonymousAttribute());

        Assert.True(operation.Responses is null || operation.Responses.Count == 0);
    }

    [Fact]
    public async Task Transform_AuthorizedAction_Adds401WithProblemDetails()
    {
        OpenApiOperation operation = new();

        await Run(operation, new AuthorizeAttribute());

        OpenApiResponse response = Assert.IsType<OpenApiResponse>(operation.Responses!["401"]);
        AssertProblemDetails(response);
        Assert.False(operation.Responses.ContainsKey("403"));
    }

    [Theory]
    [InlineData(Permissions.FilesManage)]
    [InlineData(Permissions.OrganizationMembersManage)]
    [InlineData(AuthorizationPolicies.OrganizationMember)]
    [InlineData(AuthorizationPolicies.ActiveOrganizationMember)]
    public async Task Transform_PermissionPolicy_Adds403(string policy)
    {
        OpenApiOperation operation = new();

        await Run(operation, new AuthorizeAttribute { Policy = policy });

        AssertProblemDetails(Assert.IsType<OpenApiResponse>(operation.Responses!["403"]));
        Assert.True(operation.Responses.ContainsKey("401"));
    }

    [Fact]
    public async Task Transform_SignedInUserPolicy_DoesNotAdd403()
    {
        OpenApiOperation operation = new();

        await Run(operation, new AuthorizeAttribute { Policy = AuthorizationPolicies.SignedInUser });

        Assert.True(operation.Responses!.ContainsKey("401"));
        Assert.False(operation.Responses.ContainsKey("403"));
    }

    [Fact]
    public async Task Transform_Existing401_IsKept()
    {
        OpenApiResponse existing = new() { Description = "Bad credentials" };
        OpenApiOperation operation = new() { Responses = new OpenApiResponses { ["401"] = existing } };

        await Run(operation, new AuthorizeAttribute());

        Assert.Same(existing, operation.Responses["401"]);
    }

    [Fact]
    public async Task Transform_ClassAuthorizeWithActionAllowAnonymous_AddsNothing()
    {
        OpenApiOperation operation = new();

        // Endpoint metadata lists the class attributes first, then the action's.
        await Run(operation, new AuthorizeAttribute { Policy = Permissions.FilesManage }, new AllowAnonymousAttribute());

        Assert.True(operation.Responses is null || operation.Responses.Count == 0);
    }

    private async Task Run(OpenApiOperation operation, params object[] metadata)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddOpenApi(DocumentName);
        ServiceProvider provider = services.BuildServiceProvider();

        ApiDescription description = new()
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = [.. metadata] }
        };
        OpenApiOperationTransformerContext context = new()
        {
            DocumentName = DocumentName,
            Description = description,
            ApplicationServices = provider
        };

        await _transformer.TransformAsync(operation, context, CancellationToken.None);
    }

    private static void AssertProblemDetails(OpenApiResponse response)
    {
        Assert.NotNull(response.Content);
        foreach (string mediaType in new[] { "text/plain", "application/json", "text/json" })
        {
            IOpenApiSchema schema = response.Content[mediaType].Schema!;
            Assert.NotNull(schema.Properties);
            Assert.Contains("status", schema.Properties.Keys);
            Assert.Contains("detail", schema.Properties.Keys);
        }
    }
}
