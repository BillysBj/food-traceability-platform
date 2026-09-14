using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class SampleApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsSampleContractsResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var path = root.GetProperty("paths").GetProperty("/api/v1/organizations/{organizationId}/samples");
        Assert.False(path.TryGetProperty("get", out _));
        var operation = path.GetProperty("post");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        var expectedResponses = new Dictionary<string, string>
        {
            ["201"] = "SampleResponse",
            ["400"] = "ValidationProblemDetails",
            ["401"] = "ProblemDetails",
            ["403"] = "ProblemDetails",
            ["409"] = "ProblemDetails",
        };
        foreach (var (status, schemaName) in expectedResponses)
        {
            var content = operation.GetProperty("responses").GetProperty(status).GetProperty("content");
            Assert.Contains(content.EnumerateObject(), item =>
                item.Value.GetProperty("schema").GetProperty("$ref").GetString() == $"#/components/schemas/{schemaName}");
        }

        Assert.Equal("#/components/schemas/CreateSampleRequest", operation.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            new[] { "locationId", "lotId", "quantity", "sampleNumber", "takenAt" },
            schemas.GetProperty("CreateSampleRequest").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "id", "locationId", "lotId", "sampleNumber", "status", "takenAt", "traceabilityEventId" },
            schemas.GetProperty("SampleResponse").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
