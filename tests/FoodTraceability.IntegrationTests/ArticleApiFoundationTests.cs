using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class ArticleApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsArticleContractsResponsesAndBearerSecurity()
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
            .GetProperty("/api/v1/organizations/{organizationId}/articles");
        var itemPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/articles/{articleId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(collectionPath.GetProperty("post"), ["201", "400", "401", "403", "409"]);
        AssertOperation(itemPath.GetProperty("get"), ["200", "401", "403", "404"]);

        var requestSchemaReference = collectionPath
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.NotNull(requestSchemaReference);
        var requestSchemaName = requestSchemaReference.Split('/')[^1];
        var requestProperties = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(requestSchemaName)
            .GetProperty("properties");
        Assert.True(requestProperties.TryGetProperty("productId", out _));
        Assert.True(requestProperties.TryGetProperty("articleNumber", out _));
        Assert.True(requestProperties.TryGetProperty("gtin", out _));
        Assert.False(requestProperties.TryGetProperty("organizationId", out _));
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
                $"Article operation does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
    }
}
