using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class PlatformProductApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsPlatformProductContractsResponsesAndBearerSecurity()
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
            .GetProperty("/api/v1/platform/products");
        var itemPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/platform/products/{productId}");
        var post = collectionPath.GetProperty("post");
        var get = itemPath.GetProperty("get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(post, ["201", "400", "401", "403", "409"]);
        AssertOperation(get, ["200", "401", "403", "404"]);
        AssertSchemaReference(post.GetProperty("responses"), "201", "ProductResponse");
        AssertSchemaReference(
            post.GetProperty("responses"),
            "400",
            "ValidationProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "401", "ProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "403", "ProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "409", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "200", "ProductResponse");
        AssertSchemaReference(get.GetProperty("responses"), "401", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "403", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "404", "ProblemDetails");

        var requestSchemaReference = post
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith(
            "/CreateProductRequest",
            requestSchemaReference,
            StringComparison.Ordinal);

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var requestSchema = schemas.GetProperty("CreateProductRequest");
        var requestProperties = requestSchema.GetProperty("properties");
        Assert.True(requestProperties.TryGetProperty("productCode", out _));
        Assert.True(requestProperties.TryGetProperty("name", out _));

        var responseProperties = schemas
            .GetProperty("ProductResponse")
            .GetProperty("properties");
        Assert.True(responseProperties.TryGetProperty("id", out _));
        Assert.True(responseProperties.TryGetProperty("productCode", out _));
        Assert.True(responseProperties.TryGetProperty("name", out _));
        Assert.True(responseProperties.TryGetProperty("createdAt", out _));
        Assert.True(schemas.TryGetProperty("ProblemDetails", out _));
        Assert.True(schemas.TryGetProperty("ValidationProblemDetails", out _));
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
                $"Platform product operation does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
    }

    private static void AssertSchemaReference(
        JsonElement responses,
        string statusCode,
        string expectedSchemaName)
    {
        var content = responses.GetProperty(statusCode).GetProperty("content");
        var schemaReferences = content
            .EnumerateObject()
            .Select(mediaType => mediaType.Value.GetProperty("schema").GetProperty("$ref").GetString())
            .ToArray();
        Assert.Contains(
            schemaReferences,
            reference => reference?.EndsWith(
                $"/{expectedSchemaName}",
                StringComparison.Ordinal) == true);
    }
}
