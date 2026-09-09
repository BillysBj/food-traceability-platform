using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class TraceabilityEventApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsTraceabilityEventContractsResponsesAndBearerSecurity()
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
            .GetProperty("/api/v1/organizations/{organizationId}/traceability/events");
        var itemPath = root
            .GetProperty("paths")
            .GetProperty(
                "/api/v1/organizations/{organizationId}/traceability/events/{eventId}");
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
        Assert.Equal("CreateTraceabilityEventRequest", requestSchemaName);
        var requestProperties = GetSchemaProperties(root, requestSchemaName);
        Assert.True(requestProperties.TryGetProperty("eventTypeCode", out _));
        Assert.True(requestProperties.TryGetProperty("locationId", out _));
        Assert.True(requestProperties.TryGetProperty("occurredAt", out _));
        Assert.True(requestProperties.TryGetProperty("externalReference", out _));
        Assert.True(requestProperties.TryGetProperty("description", out _));
        Assert.True(requestProperties.TryGetProperty("inputs", out _));
        Assert.True(requestProperties.TryGetProperty("outputs", out _));
        Assert.False(requestProperties.TryGetProperty("organizationId", out _));
        Assert.False(requestProperties.TryGetProperty("createdBy", out _));

        var lineProperties = GetSchemaProperties(root, "TraceabilityEventLotRequest");
        Assert.True(lineProperties.TryGetProperty("lotId", out _));
        Assert.True(lineProperties.TryGetProperty("quantity", out _));
        Assert.False(lineProperties.TryGetProperty("unitId", out _));

        AssertResponseSchema(createOperation, "201", "TraceabilityEventResponse");
        AssertResponseSchema(readOperation, "200", "TraceabilityEventResponse");
        AssertResponseSchema(createOperation, "400", "ValidationProblemDetails");
        AssertResponseSchema(createOperation, "401", "ProblemDetails");
        AssertResponseSchema(createOperation, "403", "ProblemDetails");
        AssertResponseSchema(createOperation, "409", "ProblemDetails");
        AssertResponseSchema(readOperation, "401", "ProblemDetails");
        AssertResponseSchema(readOperation, "403", "ProblemDetails");
        AssertResponseSchema(readOperation, "404", "ProblemDetails");
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
                $"Traceability event operation does not document HTTP {statusCode}.");
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
        var schemaNames = operation
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .EnumerateObject()
            .Select(content => GetSchemaName(content.Value.GetProperty("schema")))
            .ToArray();
        Assert.Contains(expectedSchemaName, schemaNames);
    }

    private static string GetSchemaName(JsonElement schema)
    {
        var reference = schema.GetProperty("$ref").GetString();
        Assert.NotNull(reference);
        return reference.Split('/')[^1];
    }
}
