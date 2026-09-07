using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class LotApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsLotContractsResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var collectionPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/lots");
        var itemPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/lots/{lotId}");
        var createOperation = collectionPath.GetProperty("post");
        var readOperation = itemPath.GetProperty("get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(createOperation, ["201", "400", "401", "403", "409"]);
        AssertOperation(readOperation, ["200", "401", "403", "404"]);

        var requestSchemaName = GetSchemaName(createOperation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema"));
        Assert.Equal("CreateLotRequest", requestSchemaName);
        var requestProperties = GetSchemaProperties(root, requestSchemaName);
        Assert.True(requestProperties.TryGetProperty("articleId", out _));
        Assert.True(requestProperties.TryGetProperty("lotNumber", out _));
        Assert.True(requestProperties.TryGetProperty("quantity", out _));
        Assert.True(requestProperties.TryGetProperty("unitCode", out _));
        Assert.False(requestProperties.TryGetProperty("organizationId", out _));
        Assert.False(requestProperties.TryGetProperty("unitId", out _));

        AssertResponseSchema(createOperation, "201", "LotResponse");
        AssertResponseSchema(readOperation, "200", "LotResponse");
        AssertProblemResponseSchema(createOperation, "400", "ValidationProblemDetails");
        AssertProblemResponseSchema(createOperation, "401", "ProblemDetails");
        AssertProblemResponseSchema(createOperation, "403", "ProblemDetails");
        AssertProblemResponseSchema(createOperation, "409", "ProblemDetails");
        AssertProblemResponseSchema(readOperation, "401", "ProblemDetails");
        AssertProblemResponseSchema(readOperation, "403", "ProblemDetails");
        AssertProblemResponseSchema(readOperation, "404", "ProblemDetails");

        var responseProperties = GetSchemaProperties(root, "LotResponse");
        Assert.True(responseProperties.TryGetProperty("unitCode", out _));
        Assert.False(responseProperties.TryGetProperty("unitId", out _));
    }

    private static void AssertOperation(
        JsonElement operation,
        IReadOnlyList<string> expectedStatusCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (var statusCode in expectedStatusCodes)
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Lot operation does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
    }

    private static JsonElement GetSchemaProperties(JsonElement root, string schemaName)
    {
        return root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(schemaName)
            .GetProperty("properties");
    }

    private static void AssertResponseSchema(
        JsonElement operation,
        string statusCode,
        string expectedSchemaName)
    {
        var response = operation
            .GetProperty("responses")
            .GetProperty(statusCode);
        var schemaNames = response
            .GetProperty("content")
            .EnumerateObject()
            .Select(content => GetSchemaName(content.Value.GetProperty("schema")))
            .ToArray();
        Assert.Contains(expectedSchemaName, schemaNames);
    }

    private static void AssertProblemResponseSchema(
        JsonElement operation,
        string statusCode,
        string expectedSchemaName)
    {
        AssertResponseSchema(operation, statusCode, expectedSchemaName);
    }

    private static string GetSchemaName(JsonElement schema)
    {
        var reference = schema.GetProperty("$ref").GetString();
        Assert.NotNull(reference);
        return reference.Split('/')[^1];
    }
}
