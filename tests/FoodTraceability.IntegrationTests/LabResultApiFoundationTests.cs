using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class LabResultApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsResultContractsResponsesAndBearerSecurityWithoutReadEndpoint()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var path = root.GetProperty("paths").GetProperty("/api/v1/organizations/{organizationId}/samples/{sampleId}/results");
        Assert.False(path.TryGetProperty("get", out _));
        var operation = path.GetProperty("post");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        var expectedResponses = new Dictionary<string, string>
        {
            ["201"] = "LabResultResponse",
            ["400"] = "ValidationProblemDetails",
            ["401"] = "ProblemDetails",
            ["403"] = "ProblemDetails",
            ["404"] = "ProblemDetails",
            ["409"] = "ProblemDetails",
        };
        foreach (var (status, schemaName) in expectedResponses)
        {
            var content = operation.GetProperty("responses").GetProperty(status).GetProperty("content");
            Assert.Contains(content.EnumerateObject(), item =>
                item.Value.GetProperty("schema").GetProperty("$ref").GetString() == $"#/components/schemas/{schemaName}");
        }

        Assert.Equal("#/components/schemas/CreateLabResultRequest", operation.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            new[] { "assessment", "measuredAt", "method", "parameterId", "value" },
            schemas.GetProperty("CreateLabResultRequest").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "assessment", "id", "measuredAt", "method", "parameterId", "sampleId", "sampleStatus", "value" },
            schemas.GetProperty("LabResultResponse").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
