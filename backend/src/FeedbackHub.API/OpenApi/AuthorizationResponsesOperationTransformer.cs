using FeedbackHub.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FeedbackHub.API.OpenApi;

/// <summary>
/// Documents 401 on every non-anonymous action, and 403 where the policy is a permission or an organization check.
/// A response an action already declares is kept.
/// </summary>
public sealed class AuthorizationResponsesOperationTransformer : IOpenApiOperationTransformer
{
    private const string UnauthorizedCode = "401";
    private const string ForbiddenCode = "403";
    private const string ProblemDetailsSchemaId = "ProblemDetails";

    // The media types MVC advertises for a [ProducesResponseType(typeof(ProblemDetails), ...)], so a covered
    // action's contract is the same as the attribute produced.
    private static readonly string[] ProblemMediaTypes = ["text/plain", "application/json", "text/json"];

    private static readonly HashSet<string> ForbiddenPolicies =
    [
        .. Permissions.All,
        AuthorizationPolicies.OrganizationMember,
        AuthorizationPolicies.ActiveOrganizationMember
    ];

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        await AddProblemResponseAsync(operation, context, UnauthorizedCode, "Unauthorized", cancellationToken);

        bool canBeForbidden = metadata.OfType<IAuthorizeData>()
            .Any(data => data.Policy is not null && ForbiddenPolicies.Contains(data.Policy));
        if (canBeForbidden)
        {
            await AddProblemResponseAsync(operation, context, ForbiddenCode, "Forbidden", cancellationToken);
        }
    }

    private static async Task AddProblemResponseAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        string code,
        string description,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= [];
        if (operation.Responses.ContainsKey(code))
        {
            return;
        }

        IOpenApiSchema schema = await GetProblemDetailsReferenceAsync(context, cancellationToken);
        OpenApiResponse response = new()
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>()
        };
        foreach (string mediaType in ProblemMediaTypes)
        {
            response.Content[mediaType] = new OpenApiMediaType { Schema = schema };
        }

        operation.Responses[code] = response;
    }

    // GetOrCreateSchemaAsync returns the schema inline; the contract refers to it through components, like every
    // other ProblemDetails response, so the generated client keeps one ProblemDetails type.
    private static async Task<IOpenApiSchema> GetProblemDetailsReferenceAsync(
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        IOpenApiSchema schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: cancellationToken);
        if (context.Document is not { } document)
        {
            return schema;
        }

        if (document.Components?.Schemas?.ContainsKey(ProblemDetailsSchemaId) != true)
        {
            document.AddComponent(ProblemDetailsSchemaId, schema);
        }

        return new OpenApiSchemaReference(ProblemDetailsSchemaId, document);
    }
}
