using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class LotBlockReleaseApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsExactBlockReleaseRouteResponseErrorsAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var path = paths.GetProperty("/api/v1/organizations/{organizationId}/lots/{lotId}/blocks/{blockId}/release");
        var operation = path.GetProperty("post");
        Assert.False(path.TryGetProperty("get", out _));
        Assert.False(paths.TryGetProperty("/api/v1/organizations/{organizationId}/lots/{lotId}/blocks/release", out _));
        Assert.False(operation.TryGetProperty("requestBody", out _));
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Equal(new[] { "blockId", "lotId", "organizationId" }, parameters
            .Select(parameter => parameter.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(parameters, parameter =>
        {
            Assert.Equal("path", parameter.GetProperty("in").GetString());
            Assert.True(parameter.GetProperty("required").GetBoolean());
        });
        var expectedResponses = new Dictionary<string, string>
        {
            ["200"] = "ReleasedLotBlockResponse",
            ["401"] = "ProblemDetails",
            ["403"] = "ProblemDetails",
            ["404"] = "ProblemDetails",
            ["409"] = "ProblemDetails",
        };
        var responses = operation.GetProperty("responses");
        Assert.False(responses.TryGetProperty("201", out _));
        foreach (var (status, schemaName) in expectedResponses)
        {
            var content = responses.GetProperty(status).GetProperty("content");
            Assert.Contains(content.EnumerateObject(), item =>
                item.Value.GetProperty("schema").GetProperty("$ref").GetString() == $"#/components/schemas/{schemaName}");
        }
        Assert.Contains("LOT_BLOCK_RELEASE_NOT_FOUND", responses.GetProperty("404").GetProperty("description").GetString());
        Assert.Contains("LOT_BLOCK_RELEASE_CONFLICT", responses.GetProperty("409").GetProperty("description").GetString());
        var properties = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("ReleasedLotBlockResponse").GetProperty("properties");
        Assert.Equal(new[] { "blockedAt", "blockedBy", "id", "lotId", "qualityStatus", "reason", "releasedAt", "releasedBy" },
            properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
