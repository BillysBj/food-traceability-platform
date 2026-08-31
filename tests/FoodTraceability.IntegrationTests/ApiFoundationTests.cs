using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FoodTraceability.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Serilog.Events;

namespace FoodTraceability.IntegrationTests;

public sealed class ApiFoundationTests
{
    [Fact]
    public async Task HealthEndpointReturnsHealthyWithoutDatabase()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SwaggerDocumentIsServedInDevelopment()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);
        using var document = JsonDocument.Parse(content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            document.RootElement
                .GetProperty("components")
                .GetProperty("securitySchemes")
                .TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task SwaggerUiIsServedInDevelopment(string path)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task SwaggerUiIsNotServedOutsideDevelopment(string path)
    {
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnhandledExceptionReturnsProblemDetails()
    {
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            ApiWebApplicationFactory.ExceptionEndpoint,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProblemDetailsHidesInternalsOutsideDevelopment()
    {
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            ApiWebApplicationFactory.ExceptionEndpoint,
            CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(ApiWebApplicationFactory.TestExceptionMessage, content, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", content, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrelationIdIsEchoedWhenProvided()
    {
        const string correlationId = "fnd-004-correlation-123";
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(correlationId, GetCorrelationId(response));
    }

    [Fact]
    public async Task CorrelationIdIsGeneratedWhenMissing()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(GetCorrelationId(response)));
    }

    [Fact]
    public async Task ProblemDetailsContainsCorrelationId()
    {
        const string correlationId = "fnd-004-problem-correlation";
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiWebApplicationFactory.ExceptionEndpoint);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await client.SendAsync(request, CancellationToken.None);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));

        Assert.Equal(correlationId, GetCorrelationId(response));
        Assert.Equal(correlationId, document.RootElement.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal("UNHANDLED_ERROR", document.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CorrelationIdAppearsInRequestLogs()
    {
        const string correlationId = "fnd-004-log-correlation";
        await using var factory = new ApiWebApplicationFactory(Environments.Production);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiWebApplicationFactory.ExceptionEndpoint);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Contains(factory.LogSink.Events, logEvent =>
            logEvent.Properties.TryGetValue(CorrelationIdMiddleware.LogPropertyName, out var value)
            && value is ScalarValue { Value: string loggedCorrelationId }
            && loggedCorrelationId == correlationId);
    }

    [Fact]
    public async Task SensitiveRequestHeadersAreNotLogged()
    {
        const string token = "fnd-004-sensitive-token";
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, CancellationToken.None);
        var renderedLogs = string.Join(
            Environment.NewLine,
            factory.LogSink.Events.Select(logEvent => logEvent.RenderMessage()));

        Assert.DoesNotContain(token, renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsafeCorrelationIdIsRejected()
    {
        const string unsafeCorrelationId = "unsafe correlation id\r\nvalue";
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, unsafeCorrelationId);

        using var response = await client.SendAsync(request, CancellationToken.None);
        var returnedCorrelationId = GetCorrelationId(response);

        Assert.NotEqual(unsafeCorrelationId, returnedCorrelationId);
        Assert.All(returnedCorrelationId, character => Assert.True(
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'));
    }

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        return Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
    }
}
