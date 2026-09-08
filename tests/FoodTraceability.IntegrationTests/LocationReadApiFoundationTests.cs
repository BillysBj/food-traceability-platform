using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class LocationReadApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsLocationReadContractsResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var collectionOperation = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/locations")
            .GetProperty("get");
        var itemOperation = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/locations/{locationId}")
            .GetProperty("get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(collectionOperation, ["200", "400", "401", "403"]);
        AssertOperation(itemOperation, ["200", "401", "403", "404"]);

        var queryParameters = collectionOperation
            .GetProperty("parameters")
            .EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => parameter.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("A query parameter name was null."))
            .ToArray();
        Assert.Equal(["page", "pageSize"], queryParameters);

        AssertResponseSchema(collectionOperation, "200", "LocationListResponse");
        AssertResponseSchema(itemOperation, "200", "LocationResponse");
        AssertResponseSchema(collectionOperation, "400", "ValidationProblemDetails");
        AssertResponseSchema(collectionOperation, "401", "ProblemDetails");
        AssertResponseSchema(collectionOperation, "403", "ProblemDetails");
        AssertResponseSchema(itemOperation, "401", "ProblemDetails");
        AssertResponseSchema(itemOperation, "403", "ProblemDetails");
        AssertResponseSchema(itemOperation, "404", "ProblemDetails");

        var locationProperties = GetSchemaProperties(root, "LocationResponse");
        foreach (var propertyName in new[]
        {
            "id",
            "organizationId",
            "name",
            "city",
            "region",
            "countryCode",
            "latitude",
            "longitude",
            "createdAt",
        })
        {
            Assert.True(
                locationProperties.TryGetProperty(propertyName, out _),
                $"LocationResponse does not document {propertyName}.");
        }

        var listProperties = GetSchemaProperties(root, "LocationListResponse");
        Assert.True(listProperties.TryGetProperty("items", out var items));
        Assert.Equal("LocationResponse", GetSchemaName(items.GetProperty("items")));
        Assert.True(listProperties.TryGetProperty("page", out _));
        Assert.True(listProperties.TryGetProperty("pageSize", out _));
        Assert.True(listProperties.TryGetProperty("totalCount", out _));
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
                $"Location read operation does not document HTTP {statusCode}.");
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

    private static string GetSchemaName(JsonElement schema)
    {
        var reference = schema.GetProperty("$ref").GetString();
        Assert.NotNull(reference);
        return reference.Split('/')[^1];
    }
}
