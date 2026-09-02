using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Application.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace FoodTraceability.IntegrationTests;

public sealed class CreateLocationApiFoundationTests
{
    [Fact]
    public async Task NameLongerThanMaximumReturns400()
    {
        await AssertValidationProblemAsync(
            new
            {
                name = new string('x', 201),
                city = "Kalamata",
                region = "Peloponnese",
                countryCode = "GR",
                latitude = 37.0389m,
                longitude = 22.1142m,
            },
            "Name");
    }

    [Fact]
    public async Task InvalidCountryCodeReturns400()
    {
        await AssertValidationProblemAsync(
            new
            {
                name = "Kalamata Mill",
                city = "Kalamata",
                region = "Peloponnese",
                countryCode = "GRC",
                latitude = 37.0389m,
                longitude = 22.1142m,
            },
            "CountryCode");
    }

    [Fact]
    public async Task LatitudeWithoutLongitudeReturns400()
    {
        await AssertValidationProblemAsync(
            new
            {
                name = "Kalamata Mill",
                city = "Kalamata",
                region = "Peloponnese",
                countryCode = "GR",
                latitude = 37.0389m,
                longitude = (decimal?)null,
            },
            "Latitude",
            "Longitude");
    }

    [Fact]
    public async Task SwaggerDocumentsCreateLocationContractResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var operation = root
            .GetProperty("paths")
            .GetProperty("/api/v1/organizations/{organizationId}/locations")
            .GetProperty("post");
        var responses = operation.GetProperty("responses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var statusCode in new[] { "201", "400", "401", "403" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Create location does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
        var requestSchemaReference = operation
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
        Assert.False(requestProperties.TryGetProperty("organizationId", out _));
    }

    private static async Task AssertValidationProblemAsync(
        object request,
        params string[] expectedErrorMembers)
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var authorization = new EffectiveAuthorization(
            userId,
            "create-location-validation@example.com",
            "Create Location",
            "Validation",
            true,
            [],
            [new OrganizationPermissionSet(organizationId, ["organization.manage"])],
            []);
        await using var factory = new ApiWebApplicationFactory(
            Environments.Development,
            configuration: null,
            configureTestServices: services =>
            {
                services.RemoveAll<IEffectiveAuthorizationStore>();
                services.AddScoped<IEffectiveAuthorizationStore>(_ =>
                    new StaticEffectiveAuthorizationStore(authorization));
            });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateAccessToken(userId));

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/locations",
            request,
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = document.RootElement.GetProperty("errors");
        foreach (var expectedErrorMember in expectedErrorMembers)
        {
            Assert.True(
                errors.TryGetProperty(expectedErrorMember, out _),
                $"Validation response does not contain an error for {expectedErrorMember}.");
        }
    }

    private static string CreateAccessToken(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                ApiWebApplicationFactory.TestJwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "FoodTraceability.Api",
            audience: "FoodTraceability.Client",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(15).UtcDateTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StaticEffectiveAuthorizationStore(EffectiveAuthorization authorization)
        : IEffectiveAuthorizationStore
    {
        public Task<EffectiveAuthorization?> ResolveAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<EffectiveAuthorization?>(
                authorization.UserId == userId ? authorization : null);
        }
    }
}
