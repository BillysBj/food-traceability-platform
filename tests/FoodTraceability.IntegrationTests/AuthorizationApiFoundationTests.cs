using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Application.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace FoodTraceability.IntegrationTests;

public sealed class AuthorizationApiFoundationTests
{
    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/organizations/00000000-0000-0000-0000-000000000001")]
    public async Task ProtectedEndpointsReturn401ProblemDetailsWithoutDatabase(string path)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "AUTHENTICATION_REQUIRED",
            document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(
            "Bearer",
            Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task PlatformAndLocationPermissionsStillReturn403ForOrganizationResource()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var authorization = new EffectiveAuthorization(
            userId,
            "authorization@example.com",
            "Authorization",
            "Test",
            true,
            ["organization.read"],
            [new OrganizationPermissionSet(organizationId, [])],
            [new LocationPermissionSet(
                organizationId,
                Guid.NewGuid(),
                ["organization.read"])]);
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

        using var response = await client.GetAsync(
            $"/api/v1/organizations/{organizationId}",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "AUTHORIZATION_DENIED",
            document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task SwaggerDocumentsBothAuthorizationEndpointsAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertGetOperation(
            paths,
            "/api/v1/me",
            ["200", "401"]);
        AssertGetOperation(
            paths,
            "/api/v1/organizations/{organizationId}",
            ["200", "401", "403"]);
    }

    private static void AssertGetOperation(
        JsonElement paths,
        string path,
        IReadOnlyList<string> expectedStatusCodes)
    {
        var operation = paths.GetProperty(path).GetProperty("get");
        var responses = operation.GetProperty("responses");

        foreach (var statusCode in expectedStatusCodes)
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Swagger operation {path} does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
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
