using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class QualityReadApiFoundationTests
{
    [Theory]
    [InlineData("lots", "samples", "SampleListResponse", "SampleListItemResponse", "QUALITY_SAMPLE_LIST_LOT_NOT_FOUND")]
    [InlineData("samples", "results", "LabResultListResponse", "LabResultListItemResponse", "QUALITY_RESULT_LIST_SAMPLE_NOT_FOUND")]
    [InlineData("lots", "blocks", "LotBlockListResponse", "LotBlockListItemResponse", "QUALITY_BLOCK_LIST_LOT_NOT_FOUND")]
    public async Task SwaggerDocumentsQualityReadPaginationContractsAndBearerSecurity(
        string parent, string child, string pageSchema, string itemSchema, string notFoundCode)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var parentId = parent == "lots" ? "lotId" : "sampleId";
        var path = $"/api/v1/organizations/{{organizationId}}/{parent}/{{{parentId}}}/{child}";
        var operation = root.GetProperty("paths").GetProperty(path).GetProperty("get");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        Assert.Contains("quality.read", operation.GetProperty("description").GetString());
        Assert.Contains(notFoundCode, operation.GetProperty("responses").GetProperty("404").GetProperty("description").GetString());
        var expectedResponses = new Dictionary<string, string>
        {
            ["200"] = pageSchema,
            ["400"] = "ValidationProblemDetails",
            ["401"] = "ProblemDetails",
            ["403"] = "ProblemDetails",
            ["404"] = "ProblemDetails",
        };
        foreach (var (status, schemaName) in expectedResponses)
        {
            var content = operation.GetProperty("responses").GetProperty(status).GetProperty("content");
            Assert.Contains(content.EnumerateObject(), item =>
                item.Value.GetProperty("schema").GetProperty("$ref").GetString() == $"#/components/schemas/{schemaName}");
        }

        var query = operation.GetProperty("parameters").EnumerateArray()
            .Where(item => item.GetProperty("in").GetString() == "query")
            .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("schema"));
        Assert.Equal(new[] { "page", "pageSize" }, query.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(1, query["page"].GetProperty("minimum").GetInt32());
        Assert.Equal(int.MaxValue, query["page"].GetProperty("maximum").GetInt32());
        Assert.Equal(1, query["pageSize"].GetProperty("minimum").GetInt32());
        Assert.Equal(100, query["pageSize"].GetProperty("maximum").GetInt32());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var page = schemas.GetProperty(pageSchema).GetProperty("properties");
        Assert.Equal(new[] { "items", "page", "pageSize", "totalCount" },
            page.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal($"#/components/schemas/{itemSchema}", page.GetProperty("items")
            .GetProperty("items").GetProperty("$ref").GetString());
        var expectedFields = child == "blocks"
            ? new[] { "blockedAt", "blockedBy", "id", "lotId", "reason", "releasedAt", "releasedBy" }
            : parent == "lots"
            ? new[] { "createdAt", "id", "locationId", "lotId", "sampleNumber", "status", "takenAt", "traceabilityEventId" }
            : new[] { "assessment", "createdAt", "id", "measuredAt", "method", "parameterId", "sampleId", "value" };
        Assert.Equal(expectedFields, schemas.GetProperty(itemSchema).GetProperty("properties")
            .EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("lots", "samples")]
    [InlineData("samples", "results")]
    [InlineData("lots", "blocks")]
    [InlineData("samples", "specification")]
    public async Task AnonymousQualityReadsRequireAuthentication(string parent, string child)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{Guid.NewGuid()}/{parent}/{Guid.NewGuid()}/{child}",
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocumentsApplicableSpecificationAndItsDistinctErrors()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var operation = root.GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/samples/{sampleId}/specification").GetProperty("get");
        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        Assert.Contains("quality.read", operation.GetProperty("description").GetString());
        Assert.DoesNotContain(operation.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("in").GetString() == "query");
        var responses = operation.GetProperty("responses");
        var notFound = responses.GetProperty("404").GetProperty("description").GetString();
        Assert.Contains("QUALITY_SPECIFICATION_SAMPLE_NOT_FOUND", notFound);
        Assert.Contains("QUALITY_APPLICABLE_SPECIFICATION_NOT_FOUND", notFound);
        Assert.Contains("QUALITY_SPECIFICATION_AMBIGUOUS", responses.GetProperty("409").GetProperty("description").GetString());
        foreach (var status in new[] { "200", "401", "403", "404", "409" })
        {
            var schema = status == "200" ? "SampleSpecificationResponse" : "ProblemDetails";
            Assert.Contains(responses.GetProperty(status).GetProperty("content").EnumerateObject(), item =>
                item.Value.GetProperty("schema").GetProperty("$ref").GetString() == $"#/components/schemas/{schema}");
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var specification = schemas.GetProperty("SampleSpecificationResponse").GetProperty("properties");
        Assert.Equal(new[] { "articleId", "id", "parameters", "validFrom", "validTo", "version" },
            specification.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("#/components/schemas/SpecificationParameterResponse",
            specification.GetProperty("parameters").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal(new[] { "maximum", "minimum", "parameterCode", "parameterId", "required", "standardMethod", "target", "unitId" },
            schemas.GetProperty("SpecificationParameterResponse").GetProperty("properties")
                .EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
