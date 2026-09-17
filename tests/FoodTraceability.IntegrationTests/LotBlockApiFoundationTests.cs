using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class LotBlockApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsBlockContractsErrorsAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var path = root.GetProperty("paths").GetProperty("/api/v1/organizations/{organizationId}/lots/{lotId}/blocks");
        Assert.False(root.GetProperty("paths").TryGetProperty(
            "/api/v1/organizations/{organizationId}/lots/{lotId}/blocks/{blockId}", out _));
        var operation = path.GetProperty("post");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        var expectedResponses = new Dictionary<string, string>
        {
            ["201"] = "LotBlockResponse",
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

        foreach (var (status, code) in new[]
                 {
                     ("400", "LOT_BLOCK_VALIDATION_FAILED"),
                     ("404", "LOT_BLOCK_LOT_NOT_FOUND"),
                     ("409", "LOT_BLOCK_CONFLICT"),
                 })
        {
            Assert.Contains(code, operation.GetProperty("responses").GetProperty(status).GetProperty("description").GetString());
        }

        Assert.Equal("#/components/schemas/BlockLotRequest", operation.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(new[] { "reason" }, schemas.GetProperty("BlockLotRequest").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "blockedAt", "blockedBy", "id", "lotId", "qualityStatus", "reason" },
            schemas.GetProperty("LotBlockResponse").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
