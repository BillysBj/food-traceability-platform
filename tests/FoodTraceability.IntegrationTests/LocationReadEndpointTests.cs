using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LocationReadEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task UnauthenticatedReadReturns401()
    {
        var organization = await CreateOrganizationAsync();
        var location = await CreateLocationAsync(organization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            LocationPath(organization.OrganizationId, location.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnauthenticatedListReturns401()
    {
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            LocationCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenReturns401ForReadAndList()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var location = await CreateLocationAsync(setup.Organization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await DeactivateUserAsync(setup.Account.UserId);

        using var readResponse = await client.GetAsync(
            LocationPath(setup.Organization.OrganizationId, location.Id),
            factory.RequestCancellationToken);
        using var listResponse = await client.GetAsync(
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            readResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            listResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutMembershipReturns403ForReadAndList()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var location = await CreateLocationAsync(organization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var readResponse = await client.GetAsync(
            LocationPath(organization.OrganizationId, location.Id),
            factory.RequestCancellationToken);
        using var listResponse = await client.GetAsync(
            LocationCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(readResponse, factory.RequestCancellationToken);
        await AssertForbiddenAsync(listResponse, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutOrganizationReadReturns403ForReadAndList()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var location = await CreateLocationAsync(organization.OrganizationId);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var readResponse = await client.GetAsync(
            LocationPath(organization.OrganizationId, location.Id),
            factory.RequestCancellationToken);
        using var listResponse = await client.GetAsync(
            LocationCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(readResponse, factory.RequestCancellationToken);
        await AssertForbiddenAsync(listResponse, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownOrganizationReturns403InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var unknownOrganizationId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var readResponse = await client.GetAsync(
            LocationPath(unknownOrganizationId, Guid.NewGuid()),
            factory.RequestCancellationToken);
        using var listResponse = await client.GetAsync(
            LocationCollectionPath(unknownOrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(readResponse, factory.RequestCancellationToken);
        await AssertForbiddenAsync(listResponse, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MemberInOrganizationACannotReadOrganizationB()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var organizationB = await CreateOrganizationAsync();
        var locationB = await CreateLocationAsync(organizationB.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var readResponse = await client.GetAsync(
            LocationPath(organizationB.OrganizationId, locationB.Id),
            factory.RequestCancellationToken);
        using var listResponse = await client.GetAsync(
            LocationCollectionPath(organizationB.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(readResponse, factory.RequestCancellationToken);
        await AssertForbiddenAsync(listResponse, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ForeignAndMissingLocationIdsReturnIndistinguishable404Responses()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignLocation = await CreateLocationAsync(foreignOrganization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var foreignResponse = await client.GetAsync(
            LocationPath(setup.Organization.OrganizationId, foreignLocation.Id),
            factory.RequestCancellationToken);
        using var missingResponse = await client.GetAsync(
            LocationPath(setup.Organization.OrganizationId, Guid.NewGuid()),
            factory.RequestCancellationToken);
        var foreignBody = RemoveCorrelationIdentifiers(
            await foreignResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var missingBody = RemoveCorrelationIdentifiers(
            await missingResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(foreignBody, missingBody);
        using var problem = JsonDocument.Parse(foreignBody);
        Assert.Equal(
            "LOCATION_NOT_FOUND",
            problem.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task LocationsFromAnotherOrganizationDoNotAppear()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var ownLocation = await CreateLocationAsync(setup.Organization.OrganizationId);
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignLocation = await CreateLocationAsync(foreignOrganization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(ownLocation.Id, item.Id);
        Assert.DoesNotContain(page.Items, location => location.Id == foreignLocation.Id);
    }

    [Fact]
    public async Task TotalCountExcludesLocationsFromAnotherOrganization()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await CreateLocationAsync(setup.Organization.OrganizationId);
        var foreignOrganization = await CreateOrganizationAsync();
        await CreateLocationAsync(foreignOrganization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task DefaultOrderIsCreatedAtDescending()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var basis = DateTimeOffset.UtcNow.AddHours(-1);
        var oldest = await CreateLocationAsync(
            setup.Organization.OrganizationId,
            createdAt: basis);
        var newest = await CreateLocationAsync(
            setup.Organization.OrganizationId,
            createdAt: basis.AddMinutes(2));
        var middle = await CreateLocationAsync(
            setup.Organization.OrganizationId,
            createdAt: basis.AddMinutes(1));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(
            [newest.Id, middle.Id, oldest.Id],
            page.Items.Select(location => location.Id));
    }

    [Fact]
    public async Task LocationIdDescendingBreaksEqualCreatedAtTies()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var ascendingIds = Enumerable.Range(1, 6)
            .Select(index => Guid.Parse($"10000000-0000-0000-0000-{index:D12}"))
            .ToArray();
        foreach (var id in ascendingIds)
        {
            await CreateLocationAsync(
                setup.Organization.OrganizationId,
                createdAt: createdAt,
                id: id);
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var actualIds = new List<Guid>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var page = await GetPageAsync(
                client,
                $"{LocationCollectionPath(setup.Organization.OrganizationId)}?page={pageNumber}&pageSize=2",
                factory.RequestCancellationToken);
            actualIds.AddRange(page.Items.Select(location => location.Id));
        }

        Assert.Equal(ascendingIds.Reverse(), actualIds);
        Assert.Equal(actualIds.Count, actualIds.Distinct().Count());
    }

    [Fact]
    public async Task OmittedPaginationUsesPageOneAndPageSizeFifty()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    [Fact]
    public async Task SecondPageContainsCorrectNonOverlappingItems()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var basis = DateTimeOffset.UtcNow.AddHours(-1);
        var locations = new List<Location>();
        for (var index = 0; index < 4; index++)
        {
            locations.Add(await CreateLocationAsync(
                setup.Organization.OrganizationId,
                createdAt: basis.AddMinutes(index)));
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var firstPage = await GetPageAsync(
            client,
            $"{LocationCollectionPath(setup.Organization.OrganizationId)}?page=1&pageSize=2",
            factory.RequestCancellationToken);
        var secondPage = await GetPageAsync(
            client,
            $"{LocationCollectionPath(setup.Organization.OrganizationId)}?page=2&pageSize=2",
            factory.RequestCancellationToken);

        Assert.Equal(
            [locations[3].Id, locations[2].Id],
            firstPage.Items.Select(location => location.Id));
        Assert.Equal(
            [locations[1].Id, locations[0].Id],
            secondPage.Items.Select(location => location.Id));
        Assert.Empty(firstPage.Items.Select(location => location.Id).Intersect(
            secondPage.Items.Select(location => location.Id)));
    }

    [Fact]
    public async Task PagePastLastItemReturnsEmptyItemsAndUnchangedTotalCount()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await CreateLocationAsync(setup.Organization.OrganizationId);
        await CreateLocationAsync(setup.Organization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LocationCollectionPath(setup.Organization.OrganizationId)}?page=3&pageSize=1",
            factory.RequestCancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task InvalidPaginationReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        foreach (var query in new[] { "page=0", "pageSize=0", "pageSize=101" })
        {
            using var response = await client.GetAsync(
                $"{LocationCollectionPath(setup.Organization.OrganizationId)}?{query}",
                factory.RequestCancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
            Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task PageSizeOneHundredIsAccepted()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LocationCollectionPath(setup.Organization.OrganizationId)}?pageSize=100",
            factory.RequestCancellationToken);

        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task EmptyOrganizationReturns200WithEmptyItems()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LocationCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ReadReturnsAllLocationFields()
    {
        var setup = await CreateAuthorizedSetupAsync();
        var location = await CreateLocationAsync(
            setup.Organization.OrganizationId,
            name: "Kalamata Mill",
            city: "Kalamata",
            region: "Peloponnese",
            countryCode: "GR",
            latitude: 37.0389m,
            longitude: 22.1142m,
            createdAt: new DateTimeOffset(2026, 9, 8, 12, 30, 0, TimeSpan.Zero));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LocationPath(setup.Organization.OrganizationId, location.Id),
            factory.RequestCancellationToken);
        var body = await ReadLocationAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(location.Id, body.Id);
        Assert.Equal(location.OrganizationId, body.OrganizationId);
        Assert.Equal(location.Name, body.Name);
        Assert.Equal(location.City, body.City);
        Assert.Equal(location.Region, body.Region);
        Assert.Equal(location.CountryCode?.Value, body.CountryCode);
        Assert.Equal(location.Latitude, body.Latitude);
        Assert.Equal(location.Longitude, body.Longitude);
        Assert.Equal(location.CreatedAt, body.CreatedAt);
    }

    [Fact]
    public async Task CreatedAtFromPostMatchesSubsequentReadExactly()
    {
        var setup = await CreateAuthorizedSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = new CreateLocationTestRequest(
            "Kalamata Mill",
            "Kalamata",
            "Peloponnese",
            "GR",
            37.0389m,
            22.1142m);

        using var createResponse = await client.PostAsJsonAsync(
            LocationCollectionPath(setup.Organization.OrganizationId),
            request,
            factory.RequestCancellationToken);
        var created = await ReadLocationAsync(
            createResponse,
            factory.RequestCancellationToken);
        using var readResponse = await client.GetAsync(
            LocationPath(setup.Organization.OrganizationId, created.Id),
            factory.RequestCancellationToken);
        var read = await ReadLocationAsync(readResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.IdentityConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<AuthorizedSetup> CreateAuthorizedSetupAsync()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.OrganizationAdmin);
        return new AuthorizedSetup(account, organization);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"location-read-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Location", "Read", now);
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

    private async Task<TestOrganization> CreateOrganizationAsync()
    {
        var organizationId = Guid.NewGuid();
        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Location Read Organization {organizationId:N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return new TestOrganization(organizationId);
    }

    private async Task<Location> CreateLocationAsync(
        Guid organizationId,
        string? name = null,
        string? city = null,
        string? region = null,
        string? countryCode = null,
        decimal? latitude = null,
        decimal? longitude = null,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        var location = Location.Create(
            id ?? Guid.NewGuid(),
            organizationId,
            name ?? $"Location {Guid.NewGuid():N}",
            city,
            region,
            countryCode is null ? null : CountryCode.Create(countryCode),
            latitude,
            longitude,
            createdAt ?? DateTimeOffset.UtcNow);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
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

    private static async Task<LocationListTestResponse> GetPageAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<LocationListTestResponse>(
            cancellationToken)
            ?? throw new InvalidOperationException("The location list response body was empty.");
    }

    private static async Task<LocationTestResponse> ReadLocationAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<LocationTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The location response body was empty.");
    }

    private static Task AssertForbiddenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            cancellationToken);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedErrorCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(
            expectedErrorCode,
            document.RootElement.GetProperty("errorCode").GetString());
    }

    private static string RemoveCorrelationIdentifiers(string responseBody)
    {
        var problemDetails = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new InvalidOperationException("The problem details response body was empty.");
        Assert.True(problemDetails.Remove("correlationId"));
        Assert.True(problemDetails.Remove("traceId"));
        return problemDetails.ToJsonString();
    }

    private static string LocationCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/locations";

    private static string LocationPath(Guid organizationId, Guid locationId) =>
        $"{LocationCollectionPath(organizationId)}/{locationId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId);

    private sealed record AuthorizedSetup(
        TestAccount Account,
        TestOrganization Organization);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateLocationTestRequest(
        string Name,
        string? City,
        string? Region,
        string? CountryCode,
        decimal? Latitude,
        decimal? Longitude);

    private sealed record LocationListTestResponse(
        IReadOnlyList<LocationTestResponse> Items,
        int Page,
        int PageSize,
        long TotalCount);

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
