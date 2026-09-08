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
public sealed class PlatformOrganizationEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task PlatformAdminCreatesOrganizationWithPlatformLocationHeader()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest($"Platform Organization {Guid.NewGuid():N}");

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            request,
            factory.RequestCancellationToken);
        var body = await ReadOrganizationAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"{PlatformOrganizationCollectionPath}/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(request.Name, body.Name);
        Assert.Equal(request.VatId, body.VatId);
        Assert.Equal(request.TaxNumber, body.TaxNumber);
        Assert.Equal(request.Email, body.Email);
        Assert.Equal(request.Phone, body.Phone);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        var persistedOrganization = await context.Organizations
            .AsNoTracking()
            .SingleAsync(organization => organization.Id == body.Id);
        Assert.Equal(request.Name, persistedOrganization.Name);
    }

    [Fact]
    public async Task CreatedOrganizationCanBeReadFromLocationHeader()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"Location Header Organization {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);
        var created = await ReadOrganizationAsync(
            createResponse,
            factory.RequestCancellationToken);
        var location = Assert.IsType<Uri>(createResponse.Headers.Location);

        using var getResponse = await client.GetAsync(
            location,
            factory.RequestCancellationToken);
        var read = await ReadOrganizationAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        // Persisted timestamps are excluded because PostgreSQL rounds to microseconds;
        // D-36 and FIX-010 define the central correction outside ORG-001.
        Assert.Equal(created.Id, read.Id);
        Assert.Equal(created.Name, read.Name);
        Assert.Equal(created.VatId, read.VatId);
        Assert.Equal(created.TaxNumber, read.TaxNumber);
        Assert.Equal(created.Email, read.Email);
        Assert.Equal(created.Phone, read.Phone);
    }

    [Fact]
    public async Task UnauthenticatedPostReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"Unauthenticated Post {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnauthenticatedGetReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            PlatformOrganizationPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenReturns401()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        await DeactivateUserAsync(account.UserId);

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"Deactivated User {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationManageOnlyInOrganizationScopeReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.Id,
            StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"Organization Scope Only {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutOrganizationManageReturns403()
    {
        var account = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"No Manage Permission {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownOrganizationReturns404WithOrganizationNotFoundCode()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            PlatformOrganizationPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "ORGANIZATION_NOT_FOUND",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MissingNameReturns400()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            new CreateOrganizationTestRequest(null, null, null, null, null),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DuplicateOrganizationNamesAreBothCreated()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var duplicateName = $"Duplicate Organization {Guid.NewGuid():N}";
        var request = ValidRequest(duplicateName);

        using var firstResponse = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            request,
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            request,
            factory.RequestCancellationToken);
        var first = await ReadOrganizationAsync(
            firstResponse,
            factory.RequestCancellationToken);
        var second = await ReadOrganizationAsync(
            secondResponse,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(duplicateName, first.Name);
        Assert.Equal(duplicateName, second.Name);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        Assert.Equal(
            2,
            await context.Organizations.CountAsync(
                organization => organization.Name == duplicateName));
    }

    [Fact]
    public async Task PlatformAccessDoesNotGrantOrganizationScopedAccessForSameOrganization()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformOrganizationCollectionPath,
            ValidRequest($"D27 Security Organization {Guid.NewGuid():N}"),
            factory.RequestCancellationToken);
        var created = await ReadOrganizationAsync(
            createResponse,
            factory.RequestCancellationToken);
        using var platformGetResponse = await client.GetAsync(
            PlatformOrganizationPath(created.Id),
            factory.RequestCancellationToken);
        var platformRead = await ReadOrganizationAsync(
            platformGetResponse,
            factory.RequestCancellationToken);
        using var organizationGetResponse = await client.GetAsync(
            OrganizationScopedPath(created.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            organizationGetResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, platformGetResponse.StatusCode);
        // Persisted timestamps are excluded because PostgreSQL rounds to microseconds;
        // D-36 and FIX-010 define the central correction outside ORG-001.
        Assert.Equal(created.Id, platformRead.Id);
        Assert.Equal(created.Name, platformRead.Name);
        Assert.Equal(created.VatId, platformRead.VatId);
        Assert.Equal(created.TaxNumber, platformRead.TaxNumber);
        Assert.Equal(created.Email, platformRead.Email);
        Assert.Equal(created.Phone, platformRead.Phone);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.IdentityConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"platform-organization-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Platform", "Test", now);
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

    private async Task<Organization> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Authorization Organization {Guid.NewGuid():N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
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

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId)
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
            locationId: null,
            now));
        await context.SaveChangesAsync();
    }

    private async Task DeactivateUserAsync(Guid userId)
    {
        await using var context = database.CreateIdentityDbContext();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        user.Deactivate(DateTimeOffset.UtcNow);
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

    private static async Task<OrganizationTestResponse> ReadOrganizationAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<OrganizationTestResponse>(
            cancellationToken)
            ?? throw new InvalidOperationException("The organization response body was empty.");
    }

    private static async Task AssertProblemAsync(
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

    private static CreateOrganizationTestRequest ValidRequest(string name) =>
        new(name, "EL123456789", "TAX-123", "office@example.com", "+30 210 1234567");

    private static string PlatformOrganizationCollectionPath =>
        "/api/v1/platform/organizations";

    private static string PlatformOrganizationPath(Guid organizationId) =>
        $"{PlatformOrganizationCollectionPath}/{organizationId}";

    private static string OrganizationScopedPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateOrganizationTestRequest(
        string? Name,
        string? VatId,
        string? TaxNumber,
        string? Email,
        string? Phone);

    private sealed record OrganizationTestResponse(
        Guid Id,
        string Name,
        string? VatId,
        string? TaxNumber,
        string? Email,
        string? Phone,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
