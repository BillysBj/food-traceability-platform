using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace FoodTraceability.IntegrationTests;

public sealed class EndpointAuthorizationGuardTests
{
    private static readonly IReadOnlySet<EndpointAuthorization> ExpectedEndpoints =
        new HashSet<EndpointAuthorization>
        {
            // Login, token renewal, and logout are authentication entry points. They must be
            // reachable before a caller can satisfy an authorization policy.
            new("POST", "/api/v1/auth/login", null),
            new("POST", "/api/v1/auth/refresh", null),
            new("POST", "/api/v1/auth/logout", null),
            new("GET", "/api/v1/me", "ActiveUser"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}",
                "OrganizationRead"),
            new(
                "POST",
                "/api/v1/organizations/{organizationId}/locations",
                "OrganizationManage"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/locations",
                "OrganizationRead"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/locations/{locationId}",
                "OrganizationRead"),
            new(
                "POST",
                "/api/v1/organizations/{organizationId}/articles",
                "ArticleCreate"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/articles/{articleId}",
                "ArticleRead"),
            new(
                "POST",
                "/api/v1/organizations/{organizationId}/lots",
                "LotCreate"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/lots",
                "LotRead"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/lots/{lotId}",
                "LotRead"),
            new(
                "POST",
                "/api/v1/organizations/{organizationId}/traceability/events",
                "TraceabilityEventCreate"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/traceability/events/{eventId}",
                "TraceabilityRead"),
            new(
                "GET",
                "/api/v1/organizations/{organizationId}/lots/{lotId}/traceability/backward",
                "TraceabilityRead"),
            new(
                "POST",
                "/api/v1/platform/organizations",
                "PlatformOrganizationManage"),
            new(
                "GET",
                "/api/v1/platform/organizations/{organizationId}",
                "PlatformOrganizationManage"),
            new(
                "POST",
                "/api/v1/platform/organizations/{organizationId}/members",
                "PlatformUserManage"),
            new(
                "GET",
                "/api/v1/platform/organizations/{organizationId}/members/{userId}",
                "PlatformUserRead"),
            new(
                "POST",
                "/api/v1/platform/organizations/{organizationId}/members/{userId}/roles",
                "PlatformUserManage"),
            new("POST", "/api/v1/platform/users", "PlatformUserManage"),
            new("GET", "/api/v1/platform/users/{userId}", "PlatformUserRead"),
            new("POST", "/api/v1/platform/products", "PlatformProductCreate"),
            new(
                "GET",
                "/api/v1/platform/products/{productId}",
                "PlatformProductRead")
        };

    [Fact]
    public void EveryEndpointCarriesTheExpectedPolicy()
    {
        var actualEndpoints = EndpointAuthorizationDiscovery.Discover();

        EndpointAuthorizationSetAssert.Equal(ExpectedEndpoints, actualEndpoints);
    }

    [Fact]
    public void OnlyTheAuthenticationEndpointsArePublic()
    {
        var expectedPublicEndpoints = ExpectedEndpoints
            .Where(static endpoint => endpoint.PolicyName is null);
        var actualPublicEndpoints = EndpointAuthorizationDiscovery
            .Discover()
            .Where(static endpoint => endpoint.PolicyName is null);

        EndpointAuthorizationSetAssert.Equal(
            expectedPublicEndpoints,
            actualPublicEndpoints);
    }
}

internal sealed record EndpointAuthorization(
    string HttpMethod,
    string Route,
    string? PolicyName)
{
    public override string ToString() =>
        $"{HttpMethod} {Route} => {PolicyName ?? "PUBLIC"}";
}

internal static class EndpointAuthorizationDiscovery
{
    private const string DefaultPolicyMarker = "<default authorization policy>";

    public static IReadOnlySet<EndpointAuthorization> Discover()
    {
        var apiAssembly = typeof(Program).Assembly;
        var endpoints = new HashSet<EndpointAuthorization>();

        // TestEndpointStartupFilter maps delegates from the test assembly. Restricting discovery
        // to ControllerBase types in the API assembly excludes those synthetic test endpoints.
        var controllerTypes = apiAssembly
            .GetTypes()
            .Where(static type =>
                !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controllerType in controllerTypes)
        {
            var controllerRoutes = controllerType
                .GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(static attribute => attribute.Template)
                .DefaultIfEmpty(string.Empty);

            foreach (var method in controllerType.GetMethods(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                var httpAttributes = method
                    .GetCustomAttributes(inherit: true)
                    .OfType<HttpMethodAttribute>()
                    .ToArray();
                if (httpAttributes.Length == 0)
                {
                    continue;
                }

                var policyName = FindEffectivePolicy(controllerType, method);
                foreach (var controllerRoute in controllerRoutes)
                {
                    foreach (var httpAttribute in httpAttributes)
                    {
                        var route = CombineRoutes(controllerRoute, httpAttribute.Template);
                        foreach (var httpMethod in httpAttribute.HttpMethods)
                        {
                            endpoints.Add(new EndpointAuthorization(
                                httpMethod.ToUpperInvariant(),
                                route,
                                policyName));
                        }
                    }
                }
            }
        }

        return endpoints;
    }

    private static string? FindEffectivePolicy(Type controllerType, MethodInfo method)
    {
        if (method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
            || controllerType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
        {
            return null;
        }

        var authorizeAttributes = method
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToArray();
        if (authorizeAttributes.Length == 0)
        {
            authorizeAttributes = controllerType
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .ToArray();
        }

        if (authorizeAttributes.Length == 0)
        {
            return null;
        }

        return string.Join(
            " & ",
            authorizeAttributes
                .Select(static attribute => string.IsNullOrWhiteSpace(attribute.Policy)
                    ? DefaultPolicyMarker
                    : attribute.Policy)
                .Order(StringComparer.Ordinal));
    }

    private static string CombineRoutes(string? controllerRoute, string? methodRoute)
    {
        var parts = new[] { controllerRoute, methodRoute }
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Select(static part => part!.Trim('/'));
        var route = $"/{string.Join('/', parts)}";

        return Regex.Replace(
            route,
            @"\{([^}:]+)(?::[^}]+)?\}",
            "{$1}",
            RegexOptions.CultureInvariant);
    }
}

internal static class EndpointAuthorizationSetAssert
{
    public static void Equal(
        IEnumerable<EndpointAuthorization> expected,
        IEnumerable<EndpointAuthorization> actual)
    {
        var expectedSet = expected.ToHashSet();
        var actualSet = actual.ToHashSet();
        var missing = expectedSet
            .Except(actualSet)
            .OrderBy(static endpoint => endpoint.Route, StringComparer.Ordinal)
            .ThenBy(static endpoint => endpoint.HttpMethod, StringComparer.Ordinal)
            .ToArray();
        var unexpected = actualSet
            .Except(expectedSet)
            .OrderBy(static endpoint => endpoint.Route, StringComparer.Ordinal)
            .ThenBy(static endpoint => endpoint.HttpMethod, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0 && unexpected.Length == 0,
            $"Missing endpoints:{Environment.NewLine}{Format(missing)}{Environment.NewLine}"
            + $"Unexpected endpoints:{Environment.NewLine}{Format(unexpected)}");
    }

    private static string Format(IReadOnlyCollection<EndpointAuthorization> endpoints) =>
        endpoints.Count == 0
            ? "  <none>"
            : string.Join(Environment.NewLine, endpoints.Select(static endpoint => $"  {endpoint}"));
}
