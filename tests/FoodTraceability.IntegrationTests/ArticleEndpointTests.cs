using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class ArticleEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task MemberWithArticleCreateCreatesArticleInRouteOrganization()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(organization.OrganizationId),
            ValidRequest(product.Id),
            factory.RequestCancellationToken);
        var body = await ReadArticleAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/v1/organizations/{organization.OrganizationId}/articles/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(organization.OrganizationId, body.OrganizationId);
        Assert.Equal(product.Id, body.ProductId);
        await using var context = database.CreateArticleApiCatalogDbContext();
        var persisted = await context.Articles
            .AsNoTracking()
            .SingleAsync(article => article.Id == body.Id);
        Assert.Equal(organization.OrganizationId, persisted.OrganizationId);
        Assert.Equal(product.Id, persisted.ProductId);
        Assert.Equal(ValidArticleNumber, persisted.ArticleNumber);
        Assert.Equal(ValidGtin, persisted.Gtin);
    }

    [Fact]
    public async Task MemberWithArticleReadReadsOwnArticle()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(organization.OrganizationId, article.Id),
            factory.RequestCancellationToken);
        var body = await ReadArticleAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(article.Id, body.Id);
        Assert.Equal(organization.OrganizationId, body.OrganizationId);
        Assert.Equal(product.Id, body.ProductId);
        Assert.Equal(article.ArticleNumber, body.ArticleNumber);
        Assert.Equal(article.Gtin, body.Gtin);
    }

    [Fact]
    public async Task UnauthenticatedCreateReturns401()
    {
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(organization.OrganizationId),
            ValidRequest(product.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnauthenticatedReadReturns401()
    {
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            ArticlePath(organization.OrganizationId, article.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenCannotCreate()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await DeactivateUserAsync(setup.Account.UserId);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Product.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenCannotRead()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer, createArticle: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await DeactivateUserAsync(setup.Account.UserId);

        using var response = await client.GetAsync(
            ArticlePath(setup.Organization.OrganizationId, setup.Article!.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task CreateWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(organization.OrganizationId),
            ValidRequest(product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ReadWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(organization.OrganizationId, article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutArticleCreateReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutArticleReadReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Logistics, createArticle: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(setup.Organization.OrganizationId, setup.Article!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedArticleCreateOnlyReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLocation: true,
            locationScoped: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedArticleReadOnlyReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.OrganizationAdmin,
            createArticle: true,
            createLocation: true,
            locationScoped: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(setup.Organization.OrganizationId, setup.Article!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformPermissionWithoutMembershipCannotCreate()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(organization.OrganizationId),
            ValidRequest(product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformPermissionWithoutMembershipCannotRead()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(organization.OrganizationId, article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task CreateForUnknownOrganizationReturns403InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(Guid.NewGuid()),
            ValidRequest(setup.Product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ReadForUnknownOrganizationReturns403InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer, createArticle: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            ArticlePath(Guid.NewGuid(), setup.Article!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MemberInOrganizationACannotCreateInOrganizationB()
    {
        var account = await CreateAccountAsync();
        var organizationA = await CreateOrganizationAsync();
        var organizationB = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organizationA.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        var totalBefore = await CountArticlesAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(organizationB.OrganizationId),
            ValidRequest(product.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
        Assert.Equal(totalBefore, await CountArticlesAsync());
        Assert.Equal(0, await CountArticlesAsync(organizationB.OrganizationId));
    }

    [Fact]
    public async Task BodyOrganizationIdCannotOverrideRouteOrganizationScope()
    {
        var account = await CreateAccountAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var injectedOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            routeOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = new Dictionary<string, object?>
        {
            ["productId"] = product.Id,
            ["articleNumber"] = UniqueArticleNumber(),
            ["gtin"] = UniqueGtin(),
            ["organizationId"] = injectedOrganization.OrganizationId,
        };

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(routeOrganization.OrganizationId),
            request,
            factory.RequestCancellationToken);
        var body = await ReadArticleAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(routeOrganization.OrganizationId, body.OrganizationId);
        await using var context = database.CreateArticleApiCatalogDbContext();
        var persisted = await context.Articles.AsNoTracking().SingleAsync(
            article => article.Id == body.Id);
        Assert.Equal(routeOrganization.OrganizationId, persisted.OrganizationId);
        Assert.False(await context.Articles.AnyAsync(
            article => article.OrganizationId == injectedOrganization.OrganizationId));
    }

    [Fact]
    public async Task UnknownProductIdReturns400InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "ARTICLE_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateArticleNumberInSameOrganizationReturns409()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var articleNumber = UniqueArticleNumber();
        await CreateArticleAsync(
            setup.Organization.OrganizationId,
            setup.Product.Id,
            articleNumber,
            UniqueGtin());
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            new CreateArticleTestRequest(setup.Product.Id, articleNumber, UniqueGtin()),
            factory.RequestCancellationToken);

        await AssertConflictAsync(
            response,
            "article number",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task SameArticleNumberInAnotherOrganizationCreatesArticle()
    {
        var account = await CreateAccountAsync();
        var existingOrganization = await CreateOrganizationAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var articleNumber = UniqueArticleNumber();
        await CreateArticleAsync(
            existingOrganization.OrganizationId,
            product.Id,
            articleNumber,
            UniqueGtin());
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            routeOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(routeOrganization.OrganizationId),
            new CreateArticleTestRequest(product.Id, articleNumber, UniqueGtin()),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateGtinInSameOrganizationReturns409()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var gtin = UniqueGtin();
        await CreateArticleAsync(
            setup.Organization.OrganizationId,
            setup.Product.Id,
            UniqueArticleNumber(),
            gtin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            new CreateArticleTestRequest(setup.Product.Id, UniqueArticleNumber(), gtin),
            factory.RequestCancellationToken);

        await AssertConflictAsync(response, "GTIN", factory.RequestCancellationToken);
    }

    [Fact]
    public async Task SameGtinInAnotherOrganizationCreatesArticle()
    {
        var account = await CreateAccountAsync();
        var existingOrganization = await CreateOrganizationAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var gtin = UniqueGtin();
        await CreateArticleAsync(
            existingOrganization.OrganizationId,
            product.Id,
            UniqueArticleNumber(),
            gtin);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            routeOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(routeOrganization.OrganizationId),
            new CreateArticleTestRequest(product.Id, UniqueArticleNumber(), gtin),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task InvalidGtinFormatReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            new CreateArticleTestRequest(setup.Product.Id, UniqueArticleNumber(), "1234A678"),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            expectedErrorCode: null,
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ForeignAndMissingArticleIdsReturnIndistinguishable404Responses()
    {
        var account = await CreateAccountAsync();
        var ownOrganization = await CreateOrganizationAsync();
        var foreignOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var foreignArticle = await CreateArticleAsync(foreignOrganization.OrganizationId, product.Id);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            ownOrganization.OrganizationId,
            StandardRoleIds.OrganizationAdmin,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var foreignResponse = await client.GetAsync(
            ArticlePath(ownOrganization.OrganizationId, foreignArticle.Id),
            factory.RequestCancellationToken);
        using var missingResponse = await client.GetAsync(
            ArticlePath(ownOrganization.OrganizationId, Guid.NewGuid()),
            factory.RequestCancellationToken);
        var foreignBody = RemoveCorrelationIdentifiers(
            await foreignResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var missingBody = RemoveCorrelationIdentifiers(
            await missingResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(foreignBody, missingBody);
    }

    [Fact]
    public async Task OrganizationAdminCanReadButCannotCreateArticle()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.OrganizationAdmin,
            createArticle: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var readResponse = await client.GetAsync(
            ArticlePath(setup.Organization.OrganizationId, setup.Article!.Id),
            factory.RequestCancellationToken);
        using var createResponse = await client.PostAsJsonAsync(
            ArticleCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Product.Id),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        await AssertForbiddenAsync(createResponse, factory.RequestCancellationToken);
    }

    private const string ValidArticleNumber = "SKU-VALID-001";
    private const string ValidGtin = "1234567890123";

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.ArticleApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<AuthorizedSetup> CreateAuthorizedSetupAsync(
        Guid roleId,
        bool createArticle = false,
        bool createLocation = false,
        bool locationScoped = false)
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync(createLocation);
        var product = await CreateProductAsync();
        var article = createArticle
            ? await CreateArticleAsync(organization.OrganizationId, product.Id)
            : null;
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.OrganizationId,
            roleId,
            locationScoped ? organization.LocationId : null);
        return new AuthorizedSetup(account, organization, product, article);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"article-api-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Article", "Test", now);
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

    private async Task<TestOrganization> CreateOrganizationAsync(bool createLocation = false)
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = createLocation ? Guid.NewGuid() : (Guid?)null;
        await using var context = database.CreateArticleApiOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Article API Organization {organizationId:N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            now));
        if (locationId is Guid id)
        {
            context.Locations.Add(Location.Create(
                id,
                organizationId,
                "Article Authorization Location",
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

    private async Task<Product> CreateProductAsync()
    {
        var id = Guid.NewGuid();
        var product = Product.Create(
            id,
            $"PRODUCT-{id:N}",
            $"Article API Product {id:N}",
            DateTimeOffset.UtcNow);
        await using var context = database.CreateArticleApiCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task<Article> CreateArticleAsync(
        Guid organizationId,
        Guid productId,
        string? articleNumber = null,
        string? gtin = null)
    {
        var article = Article.Create(
            Guid.NewGuid(),
            organizationId,
            productId,
            articleNumber ?? UniqueArticleNumber(),
            gtin ?? UniqueGtin(),
            DateTimeOffset.UtcNow);
        await using var context = database.CreateArticleApiCatalogDbContext();
        context.Articles.Add(article);
        await context.SaveChangesAsync();
        return article;
    }

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId)
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
            locationId,
            now));
        await context.SaveChangesAsync();
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

    private async Task DeactivateUserAsync(Guid userId)
    {
        await using var context = database.CreateArticleApiIdentityDbContext();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        user.Deactivate(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
    }

    private async Task<int> CountArticlesAsync(Guid? organizationId = null)
    {
        await using var context = database.CreateArticleApiCatalogDbContext();
        return organizationId is null
            ? await context.Articles.CountAsync()
            : await context.Articles.CountAsync(
                article => article.OrganizationId == organizationId.Value);
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

    private static async Task<ArticleTestResponse> ReadArticleAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<ArticleTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The article response body was empty.");
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

    private static async Task AssertConflictAsync(
        HttpResponseMessage response,
        string expectedDetailFragment,
        CancellationToken cancellationToken)
    {
        using var document = await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "ARTICLE_CONFLICT",
            cancellationToken);
        Assert.Contains(
            expectedDetailFragment,
            document.RootElement.GetProperty("detail").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonDocument> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedErrorCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        if (expectedErrorCode is not null)
        {
            Assert.Equal(
                expectedErrorCode,
                document.RootElement.GetProperty("errorCode").GetString());
        }

        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
        return document;
    }

    private static string RemoveCorrelationIdentifiers(string responseBody)
    {
        var problemDetails = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new InvalidOperationException("The problem details response body was empty.");
        Assert.True(problemDetails.Remove("correlationId"));
        Assert.True(problemDetails.Remove("traceId"));
        return problemDetails.ToJsonString();
    }

    private static CreateArticleTestRequest ValidRequest(Guid productId) =>
        new(productId, ValidArticleNumber, ValidGtin);

    private static string UniqueArticleNumber() => $"SKU-{Guid.NewGuid():N}";

    private static string UniqueGtin()
    {
        var value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) % 100_000_000_000_000;
        return value.ToString("D14", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ArticleCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/articles";

    private static string ArticlePath(Guid organizationId, Guid articleId) =>
        $"{ArticleCollectionPath(organizationId)}/{articleId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId, Guid? LocationId);

    private sealed record AuthorizedSetup(
        TestAccount Account,
        TestOrganization Organization,
        Product Product,
        Article? Article);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateArticleTestRequest(
        Guid ProductId,
        string ArticleNumber,
        string? Gtin);

    private sealed record ArticleTestResponse(
        Guid Id,
        Guid OrganizationId,
        Guid ProductId,
        string ArticleNumber,
        string? Gtin,
        DateTimeOffset CreatedAt);
}
