using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;
using FeedbackHub.Application.Common.Authorization;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Auditing;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.API.Tests.Architecture;

/// <summary>
/// Architecture rules the compiler doesn't enforce (<c>ProjectReference</c>s already fix project direction): no
/// infrastructure package in an inner layer, no Infrastructure <c>using</c> in a controller, no public entity
/// constructor, no controller action or hub without a declared access, no namespace dependency cycles.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(BaseEntity<>).Assembly;
    private static readonly Assembly Application = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_DependsOnNoOtherLayerNorPersistenceFramework() =>
        AssertNoReference(Domain, "FeedbackHub.Application", "FeedbackHub.Infrastructure", "FeedbackHub.API",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Application_DependsOnNeitherInfrastructureNorWebFramework() =>
        AssertNoReference(Application, "FeedbackHub.Infrastructure", "FeedbackHub.API",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        AssertNoReference(Infrastructure, "FeedbackHub.API");

    /// <summary>
    /// The API may touch Infrastructure only where it composes the app (DI, migrations, hub mapping). A
    /// controller or middleware using an infrastructure type directly has bypassed the Application layer.
    /// </summary>
    [Fact]
    public void Api_UsesInfrastructureOnlyInCompositionRoot()
    {
        string[] allowed = ["Program.cs"];
        string apiRoot = Path.Combine(SourceRoot(), "FeedbackHub.API");

        string[] offenders = [.. SourceFiles(apiRoot)
            .Where(file => !allowed.Contains(Path.GetRelativePath(apiRoot, file)))
            // Fully-qualified names written inline count too, not just using lines.
            .Where(file => File.ReadAllText(file).Contains("FeedbackHub.Infrastructure", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(apiRoot, file))];

        Assert.Empty(offenders);
    }

    /// <summary>Entity pattern (AGENTS.md): no public constructor, created through <c>static Create</c>.</summary>
    [Fact]
    public void Entities_HaveNoPublicConstructorAndAStaticCreateFactory()
    {
        Type[] entities = [.. Domain.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }
            && IsEntity(type))];
        Assert.NotEmpty(entities);

        string[] publicConstructors = [.. entities
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(type => type.Name)];
        string[] missingFactory = [.. entities
            .Where(type => !type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(method => method.Name == "Create" && method.ReturnType == type))
            .Select(type => type.Name)];

        Assert.Empty(publicConstructors);
        Assert.Empty(missingFactory);
    }

    /// <summary>
    /// Every controller action and hub states its access with <c>[Authorize]</c> or <c>[AllowAnonymous]</c>, on
    /// itself or its class. The fallback policy already denies a forgotten one at run time; this names it in CI.
    /// </summary>
    [Fact]
    public void ControllerActionsAndHubs_DeclareAuthorizeOrAllowAnonymous()
    {
        Type[] controllers = [.. Api.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type))];
        Assert.NotEmpty(controllers);

        string[] actions = [.. controllers
            .SelectMany(controller => controller
                // Not DeclaredOnly: an action inherited from an abstract base controller is still an action.
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.DeclaringType != typeof(object) && action.DeclaringType != typeof(ControllerBase))
                .Where(action => !action.IsSpecialName && !action.IsDefined(typeof(NonActionAttribute), inherit: true))
                .Where(action => !DeclaresAccess(action) && !DeclaresAccess(controller))
                .Select(action => $"{controller.Name}.{action.Name}"))];
        string[] hubs = [.. new[] { Api, Infrastructure }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(Hub).IsAssignableFrom(type))
            .Where(hub => !DeclaresAccess(hub))
            .Select(hub => hub.Name)];

        Assert.Empty(actions);
        Assert.Empty(hubs);
    }

    /// <summary>
    /// QĐ-06: organization administration takes the organization from the JWT <c>org_id</c>; a route naming one would
    /// let a caller checked in organization B act on organization A.
    /// </summary>
    [Fact]
    public void ControllerRoutes_NameNoOrganizationUnderApiOrganizations()
    {
        string[] offenders = [.. ControllerRoutes(ApiControllers())
            .Where(IsUnderApiOrganizations)
            .Select(route => $"{route.Action}: {route.Template}")];

        Assert.Empty(offenders);
    }

    /// <summary>Every organization-gate policy is checked in the JWT's organization: no such route names one.</summary>
    [Fact]
    public void OrganizationGatePolicies_HaveNoRouteParameterNamingAnOrganization()
    {
        string[] offenders = [.. ControllerRoutes(ApiControllers())
            .Where(IsGatedRouteNamingAnOrganization)
            .Select(route => $"{route.Action}: {route.Template}")];

        Assert.Empty(offenders);
    }

    // The scanner itself: a probe controller per shape the two route rules above must catch.
    [Theory]
    [InlineData(typeof(ClassMemberPolicyProbeController))]
    [InlineData(typeof(MemberOrNotFoundPolicyProbeController))]
    [InlineData(typeof(PermissionPolicyProbeController))]
    public void IsGatedRouteNamingAnOrganization_ProbeRoute_IsFlagged(Type probe)
    {
        RouteInfo route = Assert.Single(ControllerRoutes([probe]));

        Assert.True(IsGatedRouteNamingAnOrganization(route), route.Template);
    }

    [Fact]
    public void IsUnderApiOrganizations_ControllerToken_IsExpanded()
    {
        RouteInfo route = Assert.Single(ControllerRoutes([typeof(OrganizationsController)]));

        Assert.Equal("api/organizations/{id:guid}/members", route.Template);
        Assert.True(IsUnderApiOrganizations(route));
    }

    [Fact]
    public void ControllerRoutes_ActionToken_IsExpandedLowercase()
    {
        RouteInfo route = Assert.Single(ControllerRoutes([typeof(ActionTokenProbeController)]));

        Assert.Equal("api/organizations/{id}", route.Template);
        Assert.True(IsUnderApiOrganizations(route));
        Assert.True(IsGatedRouteNamingAnOrganization(route));
    }

    /// <summary>System-administrator routes may name an organization only because their policy ignores org_id.</summary>
    [Fact]
    public void SystemOrganizationRoutes_RequireSystemAdministratorAndNoPermissionCode()
    {
        RouteInfo[] routes = [.. ControllerRoutes(ApiControllers())
            .Where(route => route.Template.StartsWith("api/system/organizations", StringComparison.OrdinalIgnoreCase))];
        Assert.NotEmpty(routes);

        string[] offenders = [.. routes
            .Where(route => !route.Policies.Contains(AuthorizationPolicies.SystemAdministrator)
                || route.Policies.Any(Permissions.All.Contains))
            .Select(route => $"{route.Action}: {route.Template}")];

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Exceptions defined in Domain/Application must go through <see cref="AppException"/> so the middleware maps
    /// them to the right status code and localization key — a bare <see cref="Exception"/> escaping becomes a 500.
    /// </summary>
    [Fact]
    public void DomainAndApplicationExceptions_DeriveFromAppException()
    {
        string[] offenders = [.. new[] { Domain, Application }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(Exception).IsAssignableFrom(type) && !typeof(AppException).IsAssignableFrom(type))
            .Select(type => type.Name)];

        Assert.Empty(offenders);
    }

    /// <summary>
    /// No namespace cycles by <c>using</c>, grouped at <c>FeedbackHub.Layer.X.Y</c>; removing one listed edge breaks a
    /// cycle. Split at segment four (<c>Services.Sessions</c>, not <c>Services.Auth.Sessions</c>); deeper fold in.
    /// A <c>cref</c> needs a <c>using</c>, which adds an edge: write the relative name instead.
    /// </summary>
    [Fact]
    public void Namespaces_HaveNoDependencyCycles()
    {
        string[] cycleEdges = [.. NamespaceCycleEdges(SourceRoot()).Order(StringComparer.Ordinal)];

        // Assert.Empty truncates each item; print them in full so the edge to remove is obvious.
        Assert.True(cycleEdges.Length == 0, "Edges inside a dependency cycle:\n" + string.Join("\n", cycleEdges));
    }

    /// <summary>
    /// A <c>global using</c> pulls a namespace into every file of its project without showing in any of them,
    /// so the cycle check above can't attribute it to an edge.
    /// </summary>
    [Fact]
    public void Sources_HaveNoGlobalUsingOfAFeedbackHubNamespace()
    {
        string[] offenders = [.. SourceFiles(SourceRoot())
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"^\s*global\s+using\s+(?:static\s+)?(?:\w+\s*=\s*)?FeedbackHub", RegexOptions.Multiline))
            .Select(file => Path.GetRelativePath(SourceRoot(), file))];

        Assert.Empty(offenders);
    }

    /// <summary>
    /// C1: an audit event's organization comes from the entity it describes, never from a caller-supplied Guid.
    /// No public constructor, no public static method returning one that takes a <c>Guid</c> (a <c>Guid?</c> only as
    /// <c>actorAccountId</c>), and no public setter on <c>OrganizationId</c>.
    /// </summary>
    [Fact]
    public void AuditEvent_HasNoPublicConstructorOrFactoryTakingOrganizationGuid()
    {
        Type auditEvent = typeof(AuditEvent);

        Assert.Empty(auditEvent.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        string[] offenders = [.. auditEvent.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == auditEvent)
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(Guid)
                || (parameter.ParameterType == typeof(Guid?) && parameter.Name != "actorAccountId")))
            .Select(method => method.ToString() ?? method.Name)];
        Assert.Empty(offenders);

        MethodInfo? setter = auditEvent.GetProperty(nameof(AuditEvent.OrganizationId))!.SetMethod;
        Assert.True(setter is null || !setter.IsPublic);
    }

    /// <summary>The audit trail is a leaf: it names no other service namespace, so any service can record to it.</summary>
    [Fact]
    public void AuditTrailNamespace_DependsOnNoOtherServiceNamespace()
    {
        string folder = Path.Combine(SourceRoot(), "FeedbackHub.Application", "Services", "AuditTrail");
        Assert.True(Directory.Exists(folder));

        string[] offenders = [.. SourceFiles(folder)
            .SelectMany(file => UsingStatements(File.ReadAllText(file)).Select(used => (file, used)))
            .Where(item => item.used.StartsWith("FeedbackHub.Application.Services.", StringComparison.Ordinal)
                && NamespaceKey(item.used) != "FeedbackHub.Application.Services.AuditTrail")
            .Select(item => $"{Path.GetFileName(item.file)} -> {item.used}")];

        Assert.Empty(offenders);
    }

    private static void AssertNoReference(Assembly assembly, params string[] forbiddenPrefixes)
    {
        string[] offenders = [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))];

        Assert.Empty(offenders);
    }

    private sealed record RouteInfo(string Action, string Template, string[] Policies);

    private static IEnumerable<Type> ApiControllers() => Api.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type));

    private static bool IsUnderApiOrganizations(RouteInfo route) =>
        route.Template.StartsWith("api/organizations/{", StringComparison.OrdinalIgnoreCase);

    // Policies the organization gate evaluates against the JWT org_id.
    private static readonly string[] OrganizationGatePolicies =
    [
        .. Permissions.All,
        AuthorizationPolicies.ActiveOrganizationMember,
        AuthorizationPolicies.ActiveOrganizationMemberOrNotFound
    ];

    private static bool IsGatedRouteNamingAnOrganization(RouteInfo route) =>
        route.Policies.Any(OrganizationGatePolicies.Contains) && NamesAnOrganization(route.Template);

    private static IEnumerable<RouteInfo> ControllerRoutes(IEnumerable<Type> controllers)
    {
        foreach (Type controller in controllers)
        {
            // Routing is case-insensitive: lowercase the tokens so the prefix and segment rules compare like routes.
            string controllerToken = (controller.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? controller.Name[..^"Controller".Length]
                : controller.Name).ToLowerInvariant();
            string[] prefixes = [.. controller.GetCustomAttributes(inherit: true).OfType<IRouteTemplateProvider>()
                .Select(provider => provider.Template ?? string.Empty)
                .DefaultIfEmpty(string.Empty)];

            foreach (MethodInfo action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.DeclaringType != typeof(object) && action.DeclaringType != typeof(ControllerBase))
                .Where(action => !action.IsSpecialName && !action.IsDefined(typeof(NonActionAttribute), inherit: true)))
            {
                string actionToken = (action.GetCustomAttribute<ActionNameAttribute>()?.Name ?? action.Name)
                    .ToLowerInvariant();
                string[] policies = [.. new MemberInfo[] { controller, action }
                    .SelectMany(member => member.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>())
                    .Select(data => data.Policy)
                    .OfType<string>()];
                string[] templates = [.. action.GetCustomAttributes(inherit: true).OfType<IRouteTemplateProvider>()
                    .Select(provider => provider.Template ?? string.Empty)
                    .DefaultIfEmpty(string.Empty)];

                foreach (string prefix in prefixes)
                {
                    foreach (string template in templates)
                    {
                        string combined = CombineTemplates(prefix, template)
                            .Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase)
                            .Replace("[action]", actionToken, StringComparison.OrdinalIgnoreCase);
                        yield return new RouteInfo($"{controller.Name}.{action.Name}", combined, policies);
                    }
                }
            }
        }
    }

    private static string CombineTemplates(string prefix, string template)
    {
        if (template.StartsWith("~/", StringComparison.Ordinal) || template.StartsWith('/'))
        {
            return template.TrimStart('~', '/');
        }

        return string.Join('/', new[] { prefix, template }.Where(part => part.Length > 0)).Trim('/');
    }

    // A parameter names an organization by its own name or by following an "organization(s)" segment.
    private static bool NamesAnOrganization(string template)
    {
        string[] segments = template.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            Match parameter = Regex.Match(segments[i], @"^\{\**([^}:=?]+)");
            if (!parameter.Success)
            {
                continue;
            }

            string name = parameter.Groups[1].Value;
            bool followsOrganization = i > 0 && segments[i - 1].ToLowerInvariant() is "organization" or "organizations";
            if (followsOrganization
                || name.StartsWith("org", StringComparison.OrdinalIgnoreCase)
                || name.Contains("organization", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresAccess(MemberInfo member) =>
        member.GetCustomAttributes(inherit: true).Any(attribute => attribute is IAuthorizeData or IAllowAnonymous);

    private static bool IsEntity(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> NamespaceCycleEdges(string sourceRoot)
    {
        Dictionary<string, HashSet<string>> edges = [];
        foreach (string file in SourceFiles(sourceRoot))
        {
            string source = File.ReadAllText(file);
            Match declared = Regex.Match(source, @"^namespace\s+([\w.]+)", RegexOptions.Multiline);
            if (!declared.Success)
            {
                continue;
            }

            string from = NamespaceKey(declared.Groups[1].Value);
            foreach (string used in UsingStatements(source))
            {
                string to = NamespaceKey(used);
                // Only edges inside one project: across projects the ProjectReferences already forbid cycles.
                if (to != from && Project(to) is { } project && project == Project(from))
                {
                    if (!edges.TryGetValue(from, out HashSet<string>? targets))
                    {
                        edges[from] = targets = [];
                    }

                    targets.Add(to);
                }
            }
        }

        Dictionary<string, int> componentOf = [];
        List<List<string>> components = StronglyConnected(edges);
        for (int i = 0; i < components.Count; i++)
        {
            foreach (string member in components[i])
            {
                componentOf[member] = components[i].Count > 1 ? i : -1;
            }
        }

        return edges.SelectMany(edge => edge.Value.Select(to => (From: edge.Key, To: to)))
            .Where(edge => componentOf.GetValueOrDefault(edge.From, -1) is >= 0 and int component
                && componentOf.GetValueOrDefault(edge.To, -1) == component)
            .Select(edge => $"{edge.From["FeedbackHub.".Length..]} -> {edge.To["FeedbackHub.".Length..]}");
    }

    private static string NamespaceKey(string name) => string.Join('.', name.Split('.').Take(4));

    /// <summary>The project segment (<c>Application</c> in <c>FeedbackHub.Application.X</c>); null for bare <c>FeedbackHub</c>.</summary>
    private static string? Project(string name) => name.Split('.') is [_, string project, ..] ? project : null;

    /// <summary>Tarjan — strongly connected components of the namespace dependency graph.</summary>
    private static List<List<string>> StronglyConnected(Dictionary<string, HashSet<string>> edges)
    {
        Dictionary<string, int> index = [];
        Dictionary<string, int> low = [];
        Stack<string> stack = new();
        HashSet<string> onStack = [];
        List<List<string>> components = [];
        int counter = 0;

        void Visit(string node)
        {
            index[node] = low[node] = counter++;
            stack.Push(node);
            onStack.Add(node);
            foreach (string next in edges.GetValueOrDefault(node) ?? [])
            {
                if (!index.ContainsKey(next))
                {
                    Visit(next);
                    low[node] = Math.Min(low[node], low[next]);
                }
                else if (onStack.Contains(next))
                {
                    low[node] = Math.Min(low[node], index[next]);
                }
            }

            if (low[node] != index[node])
            {
                return;
            }

            List<string> component = [];
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != node);
            components.Add(component);
        }

        foreach (string node in edges.Keys.ToList())
        {
            if (!index.ContainsKey(node))
            {
                Visit(node);
            }
        }

        return components;
    }

    // Also `global using`, `using static` and aliases (`using X = FeedbackHub.Y;`) — each pulls the namespace in.
    private static IEnumerable<string> UsingStatements(string source) =>
        Regex.Matches(
                source,
                @"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:\w+\s*=\s*)?(FeedbackHub(?:\.\w+)*)",
                RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value);

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

    /// <summary><c>backend/src</c>, found by walking up from the test run directory to <c>FeedbackHub.sln</c>.</summary>
    private static string SourceRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FeedbackHub.sln")))
            {
                return Path.Combine(directory.FullName, "src");
            }
        }

        throw new InvalidOperationException("FeedbackHub.sln not found above the test run directory.");
    }

    [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMember)]
    [Route("api/files")]
    private sealed class ClassMemberPolicyProbeController : ControllerBase
    {
        [HttpGet("by-org/{organizationId}")]
        public void Get(Guid organizationId) => _ = organizationId;
    }

    [Route("api/files")]
    private sealed class MemberOrNotFoundPolicyProbeController : ControllerBase
    {
        [HttpGet("{orgId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMemberOrNotFound)]
        public void Get(Guid orgId) => _ = orgId;
    }

    [Authorize(Policy = Permissions.FilesManage)]
    [Route("api/organization")]
    private sealed class PermissionPolicyProbeController : ControllerBase
    {
        [HttpDelete("{id}")]
        public void Delete(Guid id) => _ = id;
    }

    [Authorize]
    [Route("api/[controller]")]
    private sealed class OrganizationsController : ControllerBase
    {
        [HttpGet("{id:guid}/members")]
        public void Get(Guid id) => _ = id;
    }

    [Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMember)]
    [Route("api/[action]")]
    private sealed class ActionTokenProbeController : ControllerBase
    {
        [HttpGet("{id}")]
        public void Organizations(Guid id) => _ = id;
    }
}
