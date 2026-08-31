using System.Net;
using System.Text.Json;
using FoodTraceability.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.IntegrationTests;

public sealed class ApiSecurityTests
{
    private const string AllowedOrigin = "https://allowed.example";

    [Theory]
    [InlineData("/health")]
    [InlineData(ApiWebApplicationFactory.SuccessEndpoint)]
    [InlineData(ApiWebApplicationFactory.ExceptionEndpoint)]
    public async Task SecurityHeadersArePresentOnEveryResponse(string path)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal("nosniff", GetSingleHeader(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", GetSingleHeader(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", GetSingleHeader(response, "Referrer-Policy"));
        Assert.Equal("none", GetSingleHeader(response, "X-Permitted-Cross-Domain-Policies"));
        Assert.Equal(
            SecurityHeadersMiddleware.RestrictiveContentSecurityPolicy,
            GetSingleHeader(response, SecurityHeadersMiddleware.ContentSecurityPolicyHeaderName));
    }

    [Fact]
    public async Task ServerHeaderIsRemoved()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            ApiWebApplicationFactory.SuccessEndpoint,
            CancellationToken.None);

        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task CorsRejectsUnconfiguredOrigin()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateCrossOriginRequest(AllowedOrigin);

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CorsAllowsConfiguredOrigin()
    {
        await using var factory = new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigin
            });
        using var client = factory.CreateClient();
        using var request = CreateCrossOriginRequest(AllowedOrigin);

        using var response = await client.SendAsync(request, CancellationToken.None);

        var returnedOrigin = GetSingleHeader(response, "Access-Control-Allow-Origin");
        Assert.Equal(AllowedOrigin, returnedOrigin);
        Assert.NotEqual("*", returnedOrigin);
    }

    [Fact]
    public async Task RequestsBeyondTheLimitAreRejectedWith429()
    {
        await using var factory = CreateRateLimitedFactory(permitLimit: 2);
        using var client = factory.CreateClient();

        using var firstResponse = await client.GetAsync(
            ApiWebApplicationFactory.SuccessEndpoint,
            CancellationToken.None);
        using var secondResponse = await client.GetAsync(
            ApiWebApplicationFactory.SuccessEndpoint,
            CancellationToken.None);
        using var rejectedResponse = await client.GetAsync(
            ApiWebApplicationFactory.SuccessEndpoint,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task RateLimitRejectionIsProblemDetailsWithCorrelationId()
    {
        const string correlationId = "fnd-005-rate-limit-correlation";
        await using var factory = CreateRateLimitedFactory(permitLimit: 1);
        using var client = factory.CreateClient();
        using var acceptedResponse = await client.GetAsync(
            ApiWebApplicationFactory.SuccessEndpoint,
            CancellationToken.None);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            ApiWebApplicationFactory.SuccessEndpoint);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await client.SendAsync(request, CancellationToken.None);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlationId, GetSingleHeader(response, CorrelationIdMiddleware.HeaderName));
        Assert.Equal(correlationId, document.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("RATE_LIMIT_EXCEEDED", document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task HealthEndpointsAreExemptFromRateLimiting()
    {
        const int requestCountPerEndpoint = 10;
        await using var factory = CreateRateLimitedFactory(
            permitLimit: 1,
            disableHealthChecks: true);
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/health", "/health/ready" })
        {
            for (var requestNumber = 0; requestNumber < requestCountPerEndpoint; requestNumber++)
            {
                using var response = await client.GetAsync(path, CancellationToken.None);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task HstsHeaderIsAbsentInDevelopment()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task HstsHeaderIsPresentOutsideDevelopment()
    {
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://api.foodtraceability.test")
        });

        using var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(
            GetSingleHeader(response, "Strict-Transport-Security")));
    }

    [Fact]
    public async Task SwaggerUiRemainsUsableUnderContentSecurityPolicy()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html", CancellationToken.None);
        var contentSecurityPolicy = GetSingleHeader(
            response,
            SecurityHeadersMiddleware.ContentSecurityPolicyHeaderName);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SecurityHeadersMiddleware.SwaggerContentSecurityPolicy, contentSecurityPolicy);
        Assert.NotEqual(
            SecurityHeadersMiddleware.RestrictiveContentSecurityPolicy,
            contentSecurityPolicy);
        Assert.Contains("script-src 'self' 'unsafe-inline'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("style-src 'self' 'unsafe-inline'", contentSecurityPolicy, StringComparison.Ordinal);
    }

    private static ApiWebApplicationFactory CreateRateLimitedFactory(
        int permitLimit,
        bool disableHealthChecks = false)
    {
        return new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = permitLimit.ToString(),
                ["RateLimiting:WindowSeconds"] = "60"
            },
            disableHealthChecks);
    }

    private static HttpRequestMessage CreateCrossOriginRequest(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            ApiWebApplicationFactory.SuccessEndpoint);
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static string GetSingleHeader(HttpResponseMessage response, string headerName)
    {
        return Assert.Single(response.Headers.GetValues(headerName));
    }
}
