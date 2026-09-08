using System.Net;
using System.Text.Json;

namespace FoodTraceability.IntegrationTests;

public sealed class PlatformOrganizationMemberApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsMembershipContractsResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var memberCollection = paths.GetProperty(
            "/api/v1/platform/organizations/{organizationId}/members");
        var memberItem = paths.GetProperty(
            "/api/v1/platform/organizations/{organizationId}/members/{userId}");
        var roleCollection = paths.GetProperty(
            "/api/v1/platform/organizations/{organizationId}/members/{userId}/roles");
        var addMember = memberCollection.GetProperty("post");
        var getMember = memberItem.GetProperty("get");
        var assignRole = roleCollection.GetProperty("post");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(addMember, ["201", "400", "401", "403", "409"]);
        AssertOperation(getMember, ["200", "401", "403", "404"]);
        AssertOperation(assignRole, ["201", "400", "401", "403", "404", "409"]);

        AssertRequestSchema(addMember, "AddMemberRequest");
        AssertRequestSchema(assignRole, "AssignRoleRequest");
        AssertResponseSchema(addMember, "201", "MembershipResponse");
        AssertResponseSchema(getMember, "200", "MembershipResponse");
        AssertResponseSchema(assignRole, "201", "MembershipResponse");
        AssertResponseSchema(addMember, "400", "ValidationProblemDetails");
        AssertResponseSchema(addMember, "401", "ProblemDetails");
        AssertResponseSchema(addMember, "403", "ProblemDetails");
        AssertResponseSchema(addMember, "409", "ProblemDetails");
        AssertResponseSchema(getMember, "401", "ProblemDetails");
        AssertResponseSchema(getMember, "403", "ProblemDetails");
        AssertResponseSchema(getMember, "404", "ProblemDetails");
        AssertResponseSchema(assignRole, "400", "ValidationProblemDetails");
        AssertResponseSchema(assignRole, "401", "ProblemDetails");
        AssertResponseSchema(assignRole, "403", "ProblemDetails");
        AssertResponseSchema(assignRole, "404", "ProblemDetails");
        AssertResponseSchema(assignRole, "409", "ProblemDetails");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var addProperties = schemas.GetProperty("AddMemberRequest").GetProperty("properties");
        Assert.True(addProperties.TryGetProperty("userId", out _));
        Assert.False(addProperties.TryGetProperty("locationId", out _));
        var roleProperties = schemas.GetProperty("AssignRoleRequest").GetProperty("properties");
        Assert.True(roleProperties.TryGetProperty("roleId", out _));
        Assert.False(roleProperties.TryGetProperty("locationId", out _));
        var membershipProperties = schemas
            .GetProperty("MembershipResponse")
            .GetProperty("properties");
        Assert.True(membershipProperties.TryGetProperty("organizationId", out _));
        Assert.True(membershipProperties.TryGetProperty("userId", out _));
        Assert.True(membershipProperties.TryGetProperty("createdAt", out _));
        Assert.True(membershipProperties.TryGetProperty("roles", out _));
        Assert.False(membershipProperties.TryGetProperty("locationId", out _));
        var assignedRoleProperties = schemas
            .GetProperty("AssignedRoleResponse")
            .GetProperty("properties");
        Assert.True(assignedRoleProperties.TryGetProperty("roleId", out _));
        Assert.True(assignedRoleProperties.TryGetProperty("roleCode", out _));
        Assert.True(assignedRoleProperties.TryGetProperty("createdAt", out _));
        Assert.False(assignedRoleProperties.TryGetProperty("locationId", out _));
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
                $"Membership operation does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
    }

    private static void AssertRequestSchema(JsonElement operation, string expectedSchemaName)
    {
        var schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.Equal(expectedSchemaName, GetSchemaName(schema));
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
