using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class PlatformProductEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-platform-product-password-42!";

    [Fact]
    public async Task PlatformAdminCreatesProductWithLocationHeader()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest();

        using var response = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            request,
            factory.RequestCancellationToken);
        var body = await ReadProductAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            PlatformProductPath(body.Id),
            response.Headers.Location?.OriginalString);
        Assert.Equal(request.ProductCode, body.ProductCode);
        Assert.Equal(request.Name, body.Name);
        await using var context = database.CreateArticleApiCatalogDbContext();
        var persisted = await context.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == body.Id);
        Assert.Equal(body.ProductCode, persisted.ProductCode);
        Assert.Equal(body.Name, persisted.Name);
        Assert.Equal(body.CreatedAt, persisted.CreatedAt);
    }

    [Fact]
    public async Task LocationHeaderReturnsTheCreatedProduct()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        var created = await ReadProductAsync(createResponse, factory.RequestCancellationToken);
        var location = Assert.IsType<Uri>(createResponse.Headers.Location);

        using var getResponse = await client.GetAsync(
            location,
            factory.RequestCancellationToken);
        var read = await ReadProductAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, read);
    }

    [Fact]
    public async Task UnauthenticatedPostReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            ValidRequest(),
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
            PlatformProductPath(Guid.NewGuid()),
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
            PlatformProductCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationScopedProductCreateDoesNotAuthorizePost()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.Id,
            StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ProducerOrganizationProductReadDoesNotAuthorizeGet()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateStoredProductAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.Id,
            StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            PlatformProductPath(product.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutPlatformPermissionsReturns403FromBothEndpoints()
    {
        var account = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var postResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            PlatformProductPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            postResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            getResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateProductCodeReturns409()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest();

        using var firstResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            request,
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            request,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "PRODUCT_CONFLICT",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ProductCodeUniqueIndexIsCaseInsensitive()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            new CreateProductTestRequest("ABC", "Uppercase Product"),
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            new CreateProductTestRequest("abc", "Lowercase Product"),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "PRODUCT_CONFLICT",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task StoredProductCodeCasingIsPreserved()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            new CreateProductTestRequest("Olive-Oil-EV", "Extra Virgin Olive Oil"),
            factory.RequestCancellationToken);
        var created = await ReadProductAsync(createResponse, factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            PlatformProductPath(created.Id),
            factory.RequestCancellationToken);
        var read = await ReadProductAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("Olive-Oil-EV", created.ProductCode);
        Assert.Equal("Olive-Oil-EV", read.ProductCode);
    }

    [Fact]
    public async Task MissingProductCodeReturns400()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            new CreateProductTestRequest(null, "Olive Oil"),
            factory.RequestCancellationToken);

        await AssertValidationProblemAsync(response, factory.RequestCancellationToken);
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
            PlatformProductCollectionPath,
            new CreateProductTestRequest(UniqueProductCode(), null),
            factory.RequestCancellationToken);

        await AssertValidationProblemAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownProductReturns404WithProductNotFoundCode()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            PlatformProductPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "PRODUCT_NOT_FOUND",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PostAndFollowingGetReturnExactlyTheSameCreatedAt()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformProductCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        var created = await ReadProductAsync(createResponse, factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            Assert.IsType<Uri>(createResponse.Headers.Location),
            factory.RequestCancellationToken);
        var read = await ReadProductAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.ArticleApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"platform-product-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Platform",
            "Product",
            now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword),
            now);

        await using var context = database.CreateArticleApiIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private async Task<Organization> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Platform Product Authorization {Guid.NewGuid():N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateArticleApiOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    private async Task<Product> CreateStoredProductAsync()
    {
        var product = Product.Create(
            Guid.NewGuid(),
            UniqueProductCode(),
            "Stored Platform Product",
            DateTimeOffset.UtcNow);
        await using var context = database.CreateArticleApiCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task AddPlatformRoleAsync(Guid userId, Guid roleId)
    {
        await using var context = database.CreateArticleApiIdentityDbContext();
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
        await using var context = database.CreateArticleApiIdentityDbContext();
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
        await using var context = database.CreateArticleApiIdentityDbContext();
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

    private static async Task<ProductTestResponse> ReadProductAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<ProductTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The product response body was empty.");
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
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

    private static CreateProductTestRequest ValidRequest() =>
        new(UniqueProductCode(), "Platform Product");

    private static string UniqueProductCode() => $"PRODUCT-{Guid.NewGuid():N}";

    private static string PlatformProductCollectionPath => "/api/v1/platform/products";

    private static string PlatformProductPath(Guid productId) =>
        $"{PlatformProductCollectionPath}/{productId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateProductTestRequest(string? ProductCode, string? Name);

    private sealed record ProductTestResponse(
        Guid Id,
        string ProductCode,
        string Name,
        DateTimeOffset CreatedAt);
}
