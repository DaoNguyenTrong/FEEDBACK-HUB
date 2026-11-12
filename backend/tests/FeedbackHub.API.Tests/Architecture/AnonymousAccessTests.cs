using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FeedbackHub.API.Tests.TestSupport;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Architecture;

/// <summary>
/// The controller actions that must reject a caller with no credentials, read from the route table so a new action
/// is covered without anyone listing it.
/// </summary>
public static class AuthorizedActionRows
{
    private static readonly Lazy<IReadOnlyList<(string Method, string Url, string Action)>> RowsLazy = new(Build);

    public static IReadOnlyList<(string Method, string Url, string Action)> Rows => RowsLazy.Value;

    public static IEnumerable<object[]> Data => Rows.Select(row => new object[] { row.Method, row.Url, row.Action });

    // Actions with [Authorize] or no access attribute; the latter fall to the fallback policy (needs authentication).
    // Today every action carries [Authorize], so none relies on the fallback; a new unmarked one is covered.
    public static bool RequiresAuthentication(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

    // The same route table the real host maps, built without Postgres or Hangfire: controllers are discovered
    // from the API assembly and carry the attributes the predicate reads.
    internal static IReadOnlyList<RouteEndpoint> AllControllerEndpoints()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(Program).Assembly);
        WebApplication app = builder.Build();
        app.MapControllers();

        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()];
    }

    private static List<(string Method, string Url, string Action)> Build()
    {
        List<(string Method, string Url, string Action)> rows = [];

        foreach (RouteEndpoint endpoint in AllControllerEndpoints().Where(RequiresAuthentication))
        {
            ControllerActionDescriptor descriptor = endpoint.Metadata.GetRequiredMetadata<ControllerActionDescriptor>();
            string[] methods = [.. endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []];
            if (methods.Length == 0)
            {
                throw new InvalidOperationException($"Route '{endpoint.RoutePattern.RawText}' has no HTTP method.");
            }

            string url = BuildUrl(endpoint.RoutePattern);
            foreach (string method in methods)
            {
                rows.Add((method, url, $"{descriptor.ControllerName}.{descriptor.ActionName}"));
            }
        }

        return rows;
    }

    // guid -> Guid.Empty, int/long -> 1, unconstrained -> x. Anything else is not guessed: a wrong id would
    // turn the request into a routing miss and the test would pass or fail for the wrong reason.
    private static string BuildUrl(RoutePattern pattern)
    {
        StringBuilder url = new();

        foreach (RoutePatternPathSegment segment in pattern.PathSegments)
        {
            url.Append('/');
            foreach (RoutePatternPart part in segment.Parts)
            {
                switch (part)
                {
                    case RoutePatternLiteralPart literal:
                        url.Append(literal.Content);
                        break;
                    case RoutePatternParameterPart parameter:
                        url.Append(Placeholder(pattern, parameter));
                        break;
                    case RoutePatternSeparatorPart separator:
                        url.Append(separator.Content);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Route '{pattern.RawText}' has an unsupported part '{part.GetType().Name}'.");
                }
            }
        }

        return url.ToString();
    }

    internal static string Placeholder(RoutePattern pattern, RoutePatternParameterPart parameter)
    {
        if (parameter.IsOptional || parameter.IsCatchAll)
        {
            throw new InvalidOperationException(
                $"Route '{pattern.RawText}': parameter '{parameter.Name}' is optional or catch-all; add a placeholder rule.");
        }

        string[] constraints = [.. parameter.ParameterPolicies.Select(policy => policy.Content ?? policy.ParameterPolicy?.GetType().Name ?? "?")];
        return constraints switch
        {
            [] => "x",
            ["guid"] => Guid.Empty.ToString(),
            ["int"] or ["long"] => "1",
            _ => throw new InvalidOperationException(
                $"Route '{pattern.RawText}': parameter '{parameter.Name}' has constraint '{string.Join(",", constraints)}'; "
                + "add a placeholder rule."),
        };
    }

    internal static HttpRequestMessage CreateRequest(string method, string url)
    {
        HttpRequestMessage request = new(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        return request;
    }
}

[Collection(nameof(ApiCollection))]
public sealed class AnonymousAccessTests(ApiFactoryFixture fixture)
{
    // EndpointSource_FindsAuthorizedActions
    [Fact]
    public void EndpointSource_FindsAuthorizedActions()
    {
        Assert.True(AuthorizedActionRows.Rows.Count >= 40, $"Only {AuthorizedActionRows.Rows.Count} rows were found.");
    }

    // AuthorizedAction_AnonymousCaller_Returns401
    [Theory]
    [MemberData(nameof(AuthorizedActionRows.Data), MemberType = typeof(AuthorizedActionRows))]
    public async Task AuthorizedAction_AnonymousCaller_Returns401(string method, string url, string action)
    {
        HttpClient client = fixture.CreateTestClient();
        using HttpRequestMessage request = AuthorizedActionRows.CreateRequest(method, url);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"{action}: {method} {url} answered {(int)response.StatusCode} to an anonymous caller.");
    }

    // AuthorizedActions_CarryNoRateLimit
    [Fact]
    public void AuthorizedActions_CarryNoRateLimit()
    {
        string[] limited = [.. AuthorizedActionRows.AllControllerEndpoints()
            .Where(AuthorizedActionRows.RequiresAuthentication)
            .Where(endpoint => endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null)
            .Select(endpoint => endpoint.DisplayName ?? "?")];

        Assert.Empty(limited);
    }

    [Collection(nameof(ApiCollection))]
    public sealed class SignedInCaller(ApiFactoryFixture fixture) : IAsyncLifetime
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public Task DisposeAsync() => fixture.ResetAsync();

        // A member of one organization holding no permission, so each request reaches the action's own checks.
        private async Task<HttpClient> CreateMemberClientAsync()
        {
            HttpClient client = fixture.CreateTestClient();
            Account account;
            Organization organization;
            using (IServiceScope scope = fixture.Services.CreateScope())
            {
                AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                account = await AuthTestHelper.SeedConfirmedAccountAsync(
                    context, username: "route-member", email: "route-member@example.com");
                organization = await AuthTestHelper.SeedOrganizationAsync(context);
                await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, account.Id, SystemRoleKind.Member);
            }

            client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));
            return client;
        }

        // AuthorizedAction_SignedInCaller_RouteMatches
        [Theory]
        [MemberData(nameof(AuthorizedActionRows.Data), MemberType = typeof(AuthorizedActionRows))]
        public async Task AuthorizedAction_SignedInCaller_RouteMatches(string method, string url, string action)
        {
            HttpClient client = await CreateMemberClientAsync();
            using HttpRequestMessage request = AuthorizedActionRows.CreateRequest(method, url);

            HttpResponseMessage response = await client.SendAsync(request);

            Assert.True(response.StatusCode != HttpStatusCode.TooManyRequests, $"{action}: {method} {url} was rate limited.");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // A routing miss has an empty body; a domain not-found carries a ProblemDetails with an error code.
                string body = await response.Content.ReadAsStringAsync();
                Assert.False(string.IsNullOrEmpty(body), $"{action}: {method} {url} missed the route table (bare 404).");
                using JsonDocument document = JsonDocument.Parse(body);
                Assert.True(
                    document.RootElement.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.String,
                    $"{action}: {method} {url} answered 404 without a domain error code.");
            }
        }
    }
}
