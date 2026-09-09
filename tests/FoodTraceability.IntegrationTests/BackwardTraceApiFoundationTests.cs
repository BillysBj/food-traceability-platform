using System.Net;
using System.Text.Json;
using FoodTraceability.Modules.Traceability.Infrastructure.Traces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FoodTraceability.IntegrationTests;

public sealed class BackwardTraceApiFoundationTests
{
    private const string Route =
        "/api/v1/organizations/{organizationId}/lots/{lotId}/traceability/backward";

    [Fact]
    public async Task SwaggerDocumentsGraphContractErrorsAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty(Route).GetProperty("get");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        foreach (var code in new[] { "200", "401", "403", "404", "409" })
        {
            var schemaReferences = operation.GetProperty("responses").GetProperty(code)
                .GetProperty("content").EnumerateObject()
                .Select(content => content.Value.GetProperty("schema").GetProperty("$ref").GetString());
            Assert.Contains(
                "#/components/schemas/" + (code == "200" ? "TraceGraphResponse" : "ProblemDetails"),
                schemaReferences);
        }

        AssertProperties(root, "TraceGraphResponse", ["rootLotId", "nodes", "edges"]);
        AssertProperties(root, "TraceNodeResponse", ["lotId", "lotNumber", "articleId", "quantity", "unitCode"]);
        AssertProperties(root, "TraceEdgeResponse", ["eventId", "eventTypeCode", "occurredAt", "fromLotId", "toLotId"]);
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var graph = schemas.GetProperty("TraceGraphResponse").GetProperty("properties");
        Assert.Equal("#/components/schemas/TraceNodeResponse",
            graph.GetProperty("nodes").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/TraceEdgeResponse",
            graph.GetProperty("edges").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal(1000, factory.Services.GetRequiredService<IOptions<TraceGraphOptions>>().Value.MaxNodes);
    }

    [Fact]
    public async Task AnonymousRequestReturns401()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{Guid.NewGuid()}/lots/{Guid.NewGuid()}/traceability/backward",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        Assert.Equal("AUTHENTICATION_REQUIRED", document.RootElement.GetProperty("errorCode").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task NonpositiveNodeLimitFailsAtStartup(string maxNodes)
    {
        await using var factory = new ApiWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Traceability:Graph:MaxNodes"] = maxNodes,
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Traceability:Graph:MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertProperties(JsonElement root, string schema, string[] expected)
    {
        var properties = root.GetProperty("components").GetProperty("schemas").GetProperty(schema)
            .GetProperty("properties").EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), properties);
    }
}
