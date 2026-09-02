using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class CreateLocationEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task OrganizationManagerCreatesLocationInRouteOrganization()
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

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);
        var body = await ReadLocationAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/v1/organizations/{organization.OrganizationId}/locations/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(organization.OrganizationId, body.OrganizationId);
        Assert.Equal(ValidRequest.Name, body.Name);
        Assert.Equal(ValidRequest.City, body.City);
        Assert.Equal(ValidRequest.Region, body.Region);
        Assert.Equal(ValidRequest.CountryCode, body.CountryCode);
        Assert.Equal(ValidRequest.Latitude, body.Latitude);
        Assert.Equal(ValidRequest.Longitude, body.Longitude);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        var persistedLocation = await context.Locations
            .AsNoTracking()
            .SingleAsync(location => location.Id == body.Id);
        Assert.Equal(organization.OrganizationId, persistedLocation.OrganizationId);
        Assert.Equal(ValidRequest.Name, persistedLocation.Name);
        Assert.Equal(ValidRequest.City, persistedLocation.City);
        Assert.Equal(ValidRequest.Region, persistedLocation.Region);
        Assert.Equal(ValidRequest.CountryCode, persistedLocation.CountryCode?.Value);
        Assert.Equal(ValidRequest.Latitude, persistedLocation.Latitude);
        Assert.Equal(ValidRequest.Longitude, persistedLocation.Longitude);
    }

    [Fact]
    public async Task UnauthenticatedRequestReturns401()
    {
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenReturns401()
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
        await using (var context = database.CreateIdentityDbContext())
        {
            var user = await context.Users.SingleAsync(candidate => candidate.Id == account.UserId);
            user.Deactivate(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutMembershipInRouteOrganizationReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutOrganizationManageReturns403()
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

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedOrganizationManageOnlyReturns403()
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

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformOrganizationManageWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organization.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        await AssertAuthorizationProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownOrganizationReturns403InsteadOf404()
    {
        var account = await CreateAccountAsync();
        var knownOrganization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            knownOrganization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(Guid.NewGuid()),
            ValidRequest,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationManagerCannotCreateLocationInAnotherOrganization()
    {
        var account = await CreateAccountAsync();
        var organizationA = await CreateOrganizationAsync();
        var organizationB = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organizationA.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        var locationCountBeforeRequest = await CountLocationsAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(organizationB.OrganizationId),
            ValidRequest,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        Assert.Equal(locationCountBeforeRequest, await context.Locations.CountAsync());
        Assert.False(await context.Locations.AnyAsync(
            location => location.OrganizationId == organizationB.OrganizationId));
    }

    [Fact]
    public async Task BodyOrganizationIdCannotOverrideRouteOrganizationScope()
    {
        var account = await CreateAccountAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var injectedOrganization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            routeOrganization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        var routeLocationCountBeforeRequest = await CountLocationsAsync(
            routeOrganization.OrganizationId);
        var injectedLocationCountBeforeRequest = await CountLocationsAsync(
            injectedOrganization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = new Dictionary<string, object?>
        {
            ["name"] = ValidRequest.Name,
            ["city"] = ValidRequest.City,
            ["region"] = ValidRequest.Region,
            ["countryCode"] = ValidRequest.CountryCode,
            ["latitude"] = ValidRequest.Latitude,
            ["longitude"] = ValidRequest.Longitude,
            ["organizationId"] = injectedOrganization.OrganizationId,
        };

        using var response = await client.PostAsJsonAsync(
            LocationCollectionPath(routeOrganization.OrganizationId),
            request,
            factory.RequestCancellationToken);
        var body = await ReadLocationAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(routeOrganization.OrganizationId, body.OrganizationId);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        var persistedLocation = await context.Locations
            .AsNoTracking()
            .SingleAsync(location => location.Id == body.Id);
        Assert.Equal(routeOrganization.OrganizationId, persistedLocation.OrganizationId);
        Assert.Equal(
            routeLocationCountBeforeRequest + 1,
            await context.Locations.CountAsync(
                location => location.OrganizationId == routeOrganization.OrganizationId));
        Assert.Equal(
            injectedLocationCountBeforeRequest,
            await context.Locations.CountAsync(
                location => location.OrganizationId == injectedOrganization.OrganizationId));
    }

    private static CreateLocationTestRequest ValidRequest { get; } = new(
        "Kalamata Mill",
        "Kalamata",
        "Peloponnese",
        "GR",
        37.0389m,
        22.1142m);

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
        var email = $"create-location-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Create Location",
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

        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Create Location Organization {organizationId:N}",
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
                "Authorization Scope Location",
                city: null,
                region: null,
                countryCode: null,
                latitude: null,
                longitude: null,
                now));
        }

        await context.SaveChangesAsync();
        return new TestOrganization(organizationId, locationId);
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

    private async Task AddPlatformRoleAsync(Guid userId, Guid roleId)
    {
        await using var context = database.CreateIdentityDbContext();
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            userId,
            roleId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task<int> CountLocationsAsync(Guid? organizationId = null)
    {
        await using var context = database.CreateIdentityOrganizationsDbContext();
        return organizationId is null
            ? await context.Locations.CountAsync()
            : await context.Locations.CountAsync(
                location => location.OrganizationId == organizationId.Value);
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

    private static async Task<LocationTestResponse> ReadLocationAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<LocationTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The location response body was empty.");
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
    }

    private static string LocationCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/locations";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId, Guid? LocationId);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateLocationTestRequest(
        string Name,
        string City,
        string Region,
        string CountryCode,
        decimal Latitude,
        decimal Longitude);

    private sealed record LocationTestResponse(
        Guid Id,
        Guid OrganizationId,
        string Name,
        string? City,
        string? Region,
        string? CountryCode,
        decimal? Latitude,
        decimal? Longitude,
        DateTimeOffset CreatedAt);
}
