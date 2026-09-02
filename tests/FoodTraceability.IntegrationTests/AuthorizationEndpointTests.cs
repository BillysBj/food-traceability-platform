using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class AuthorizationEndpointTests(PostgreSqlContainerFixture database)
{
    private const string OrganizationReadPermission = "organization.read";
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task OrganizationEndpointWithoutTokenReturns401ProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            OrganizationPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task InvalidTokenReturns401ProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "not-a-valid-jwt");

        using var response = await client.GetAsync(
            OrganizationPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MissingOrganizationMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutRequiredPermissionReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithOrganizationWidePermissionReturnsOrganization()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(organization.OrganizationId),
            factory.RequestCancellationToken);
        var body = await response.Content.ReadFromJsonAsync<OrganizationTestResponse>(
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organization.OrganizationId, body.Id);
        Assert.Equal(organization.Name, body.Name);
    }

    [Fact]
    public async Task PermissionInOrganizationADoesNotGrantAccessToOrganizationB()
    {
        var account = await CreateAccountAsync();
        var organizationA = await CreateOrganizationAsync();
        var organizationB = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organizationA.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(organizationB.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformPermissionIsSeparateAndDoesNotGrantOrganizationAccess()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var meResponse = await client.GetAsync(
            "/api/v1/me",
            factory.RequestCancellationToken);
        var me = await ReadMeAsync(meResponse, factory.RequestCancellationToken);
        using var organizationResponse = await client.GetAsync(
            OrganizationPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Contains(OrganizationReadPermission, me.PlatformPermissions);
        Assert.DoesNotContain(
            OrganizationReadPermission,
            Assert.Single(me.OrganizationPermissions).Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, organizationResponse.StatusCode);
    }

    [Fact]
    public async Task LocationOnlyPermissionDoesNotGrantOrganizationWideAccess()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync(createLocation: true);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            organization.LocationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUserIsRejectedWithSameStillCryptographicallyValidToken()
    {
        var account = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        await using (var context = database.CreateIdentityDbContext())
        {
            var user = await context.Users.SingleAsync(
                candidate => candidate.Id == account.UserId);
            user.Deactivate(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        using var response = await client.GetAsync(
            "/api/v1/me",
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task TokenSubjectForUnknownUserReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateAccessToken(Guid.NewGuid()));

        using var response = await client.GetAsync(
            "/api/v1/me",
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MeWithoutTokenReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/me",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeSeparatesPlatformOrganizationAndLocationPermissionSources()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync(createLocation: true);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        await AddOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer,
            organization.LocationId);
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            "/api/v1/me",
            factory.RequestCancellationToken);
        var me = await ReadMeAsync(response, factory.RequestCancellationToken);

        Assert.Equal(account.UserId, me.UserId);
        Assert.Equal(account.Email, me.Email);
        Assert.Contains(OrganizationReadPermission, me.PlatformPermissions);
        Assert.DoesNotContain("lot.read", me.PlatformPermissions);
        var organizationPermissions = Assert.Single(me.OrganizationPermissions);
        Assert.Equal(organization.OrganizationId, organizationPermissions.OrganizationId);
        Assert.Contains(OrganizationReadPermission, organizationPermissions.Permissions);
        Assert.DoesNotContain("lot.read", organizationPermissions.Permissions);
        var locationPermissions = Assert.Single(me.LocationPermissions);
        Assert.Equal(organization.OrganizationId, locationPermissions.OrganizationId);
        Assert.Equal(organization.LocationId, locationPermissions.LocationId);
        Assert.Contains("lot.read", locationPermissions.Permissions);
        Assert.DoesNotContain(OrganizationReadPermission, locationPermissions.Permissions);
    }

    [Fact]
    public async Task UnknownOrganizationReturns403InsteadOf404()
    {
        var account = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            OrganizationPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private ApiWebApplicationFactory CreateFactory()
    {
        return new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.IdentityConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"authorization-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Authorization",
            "Test",
            now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword),
            now);

        await using var context = database.CreateIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private async Task<TestOrganization> CreateOrganizationAsync(bool createLocation = false)
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = createLocation ? Guid.NewGuid() : (Guid?)null;
        var name = $"Authorization Organization {organizationId:N}";

        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            name,
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            now));
        if (locationId is not null)
        {
            context.Locations.Add(Location.Create(
                locationId.Value,
                organizationId,
                "Authorization Location",
                city: null,
                region: null,
                countryCode: null,
                latitude: null,
                longitude: null,
                now));
        }

        await context.SaveChangesAsync();
        return new TestOrganization(organizationId, locationId, name);
    }

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateIdentityDbContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Create(
            userId,
            organizationId,
            now));
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            userId,
            organizationId,
            roleId,
            locationId,
            now));
        await context.SaveChangesAsync();
    }

    private async Task AddOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId)
    {
        await using var context = database.CreateIdentityDbContext();
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            userId,
            organizationId,
            roleId,
            locationId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task AddPlatformRoleAsync(Guid userId, Guid roleId)
    {
        await using var context = database.CreateIdentityDbContext();
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            userId,
            roleId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        TestAccount account,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { account.Email, Password = ValidPassword },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);
    }

    private static async Task<MeTestResponse> ReadMeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<MeTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The me response body was empty.");
    }

    private static async Task AssertAuthorizationProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedErrorCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(
            expectedErrorCode,
            document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
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

    private static string OrganizationPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId, Guid? LocationId, string Name);

    private sealed record TokenResponse(string AccessToken);

    private sealed record OrganizationTestResponse(Guid Id, string Name);

    private sealed record MeTestResponse(
        Guid UserId,
        string Email,
        IReadOnlyList<string> PlatformPermissions,
        IReadOnlyList<OrganizationPermissionTestResponse> OrganizationPermissions,
        IReadOnlyList<LocationPermissionTestResponse> LocationPermissions);

    private sealed record OrganizationPermissionTestResponse(
        Guid OrganizationId,
        IReadOnlyList<string> Permissions);

    private sealed record LocationPermissionTestResponse(
        Guid OrganizationId,
        Guid LocationId,
        IReadOnlyList<string> Permissions);
}
