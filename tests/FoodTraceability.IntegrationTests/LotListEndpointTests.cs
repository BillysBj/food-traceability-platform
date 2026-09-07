using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LotListEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    private static readonly Guid GramId =
        Guid.Parse("5e726b86-c672-5ed0-9601-904328038341");

    private static readonly Guid KilogramId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    [Fact]
    public async Task UnauthenticatedListReturns401()
    {
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            LotCollectionPath(organization.OrganizationId),
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
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await DeactivateUserAsync(setup.Account.UserId);

        using var response = await client.GetAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutLotReadReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAsync(account.UserId, organization.OrganizationId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedLotReadReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLocation: true,
            locationScoped: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformRoleWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownOrganizationReturns403InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationAdminReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LotsFromAnotherOrganizationDoNotAppear()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var ownLot = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id);
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignArticle = await CreateArticleAsync(
            foreignOrganization.OrganizationId,
            setup.Product.Id);
        var foreignLot = await CreateLotAsync(
            foreignOrganization.OrganizationId,
            foreignArticle.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(ownLot.Id, item.Id);
        Assert.DoesNotContain(page.Items, lot => lot.Id == foreignLot.Id);
    }

    [Fact]
    public async Task TotalCountExcludesLotsFromAnotherOrganization()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignArticle = await CreateArticleAsync(
            foreignOrganization.OrganizationId,
            setup.Product.Id);
        await CreateLotAsync(foreignOrganization.OrganizationId, foreignArticle.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task DefaultOrderIsCreatedAtDescending()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var basis = DateTimeOffset.UtcNow.AddHours(-1);
        var oldest = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            createdAt: basis);
        var newest = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            createdAt: basis.AddMinutes(2));
        var middle = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            createdAt: basis.AddMinutes(1));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal([newest.Id, middle.Id, oldest.Id], page.Items.Select(lot => lot.Id));
    }

    [Fact]
    public async Task LotIdDescendingBreaksEqualCreatedAtTies()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var ascendingIds = Enumerable.Range(1, 6)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:D12}"))
            .ToArray();
        foreach (var id in ascendingIds)
        {
            await CreateLotAsync(
                setup.Organization.OrganizationId,
                setup.Article.Id,
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
                $"{LotCollectionPath(setup.Organization.OrganizationId)}?page={pageNumber}&pageSize=2",
                factory.RequestCancellationToken);

            Assert.Equal(6, page.TotalCount);
            actualIds.AddRange(page.Items.Select(lot => lot.Id));
        }

        var expectedIds = ascendingIds.Reverse().ToArray();
        Assert.Equal(expectedIds, actualIds);
        Assert.Equal(actualIds.Count, actualIds.Distinct().Count());
        Assert.Empty(expectedIds.Except(actualIds));
    }

    [Fact]
    public async Task OmittedPaginationUsesPageOneAndPageSizeFifty()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    [Fact]
    public async Task SecondPageContainsCorrectNonOverlappingItems()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var basis = DateTimeOffset.UtcNow.AddHours(-1);
        var lots = new List<Lot>();
        for (var index = 0; index < 4; index++)
        {
            lots.Add(await CreateLotAsync(
                setup.Organization.OrganizationId,
                setup.Article.Id,
                createdAt: basis.AddMinutes(index)));
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var firstPage = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?page=1&pageSize=2",
            factory.RequestCancellationToken);
        var secondPage = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?page=2&pageSize=2",
            factory.RequestCancellationToken);

        Assert.Equal([lots[3].Id, lots[2].Id], firstPage.Items.Select(lot => lot.Id));
        Assert.Equal([lots[1].Id, lots[0].Id], secondPage.Items.Select(lot => lot.Id));
        Assert.Empty(firstPage.Items.Select(lot => lot.Id).Intersect(
            secondPage.Items.Select(lot => lot.Id)));
    }

    [Fact]
    public async Task PagePastLastItemReturnsEmptyItemsAndUnchangedTotalCount()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?page=3&pageSize=1",
            factory.RequestCancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task TotalCountIsFilteredTotalRatherThanPageItemCount()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        for (var index = 0; index < 3; index++)
        {
            await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?pageSize=2",
            factory.RequestCancellationToken);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task ArticleIdFilterUsesExactMatch()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var otherArticle = await CreateArticleAsync(
            setup.Organization.OrganizationId,
            setup.Product.Id);
        var matchingLot = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id);
        await CreateLotAsync(setup.Organization.OrganizationId, otherArticle.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?articleId={setup.Article.Id}",
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(matchingLot.Id, item.Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task LotNumberFilterUsesExactMatch()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var exactNumber = $"EXACT-{Guid.NewGuid():N}";
        var matchingLot = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            exactNumber);
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            $"{exactNumber}-SUFFIX");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?lotNumber={exactNumber}",
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(matchingLot.Id, item.Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task LotNumberFilterIgnoresCasing()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lotNumber = $"MiXeD-{Guid.NewGuid():N}";
        var lot = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            lotNumber);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?lotNumber={lotNumber.ToLowerInvariant()}",
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(lot.Id, item.Id);
    }

    [Fact]
    public async Task ArticleIdAndLotNumberFiltersUseAndSemantics()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var otherArticle = await CreateArticleAsync(
            setup.Organization.OrganizationId,
            setup.Product.Id);
        var targetNumber = $"TARGET-{Guid.NewGuid():N}";
        var matchingLot = await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            targetNumber);
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            otherArticle.Id,
            $"OTHER-{Guid.NewGuid():N}");
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            $"SECOND-{Guid.NewGuid():N}");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?articleId={setup.Article.Id}&lotNumber={targetNumber}",
            factory.RequestCancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(matchingLot.Id, item.Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task FilterWithoutMatchReturnsEmptyPage()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?lotNumber=DOES-NOT-EXIST",
            factory.RequestCancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task PageZeroReturns400()
    {
        await AssertInvalidQueryReturns400Async("page=0");
    }

    [Fact]
    public async Task PageSizeZeroReturns400()
    {
        await AssertInvalidQueryReturns400Async("pageSize=0");
    }

    [Fact]
    public async Task PageSizeAboveMaximumReturns400()
    {
        await AssertInvalidQueryReturns400Async("pageSize=101");
    }

    [Fact]
    public async Task PageSizeOneHundredIsAccepted()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var page = await GetPageAsync(
            client,
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?pageSize=100",
            factory.RequestCancellationToken);

        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task ItemsContainUnitCodeAndDoNotExposeUnitId()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await CreateLotAsync(setup.Organization.OrganizationId, setup.Article.Id);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("KG", item.GetProperty("unitCode").GetString());
        Assert.False(item.TryGetProperty("unitId", out _));
    }

    [Fact]
    public async Task UnitCodesForPageUseSingleCatalogQuery()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            unitId: KilogramId);
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            unitId: GramId);
        var commandCounter = new CountingCommandInterceptor();
        await using var factory = CreateFactory(services =>
            services.AddDbContext<CatalogDbContext>((_, options) =>
                options.AddInterceptors(commandCounter)));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        commandCounter.Reset();

        var page = await GetPageAsync(
            client,
            LotCollectionPath(setup.Organization.OrganizationId),
            factory.RequestCancellationToken);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, commandCounter.ReaderCommandCount);
        Assert.Equal(["G", "KG"], page.Items.Select(lot => lot.UnitCode).Order());
    }

    private ApiWebApplicationFactory CreateFactory(
        Action<IServiceCollection>? configureTestServices = null) =>
        new(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            },
            configureTestServices: configureTestServices);

    private async Task<AuthorizedSetup> CreateAuthorizedSetupAsync(
        Guid roleId,
        bool createLocation = false,
        bool locationScoped = false)
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync(createLocation);
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
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
        var email = $"lot-list-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Lot", "List", now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword),
            now);

        await using var context = database.CreateLotApiIdentityDbContext();
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
        await using var context = database.CreateLotApiOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Lot List Organization {organizationId:N}",
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
                "Lot List Location",
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
            $"Lot List Product {id:N}",
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task<Article> CreateArticleAsync(Guid organizationId, Guid productId)
    {
        var id = Guid.NewGuid();
        var article = Article.Create(
            id,
            organizationId,
            productId,
            $"SKU-{id:N}",
            gtin: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiCatalogDbContext();
        context.Articles.Add(article);
        await context.SaveChangesAsync();
        return article;
    }

    private async Task<Lot> CreateLotAsync(
        Guid organizationId,
        Guid articleId,
        string? lotNumber = null,
        DateTimeOffset? createdAt = null,
        Guid? unitId = null,
        Guid? id = null)
    {
        var lot = Lot.Create(
            id ?? Guid.NewGuid(),
            organizationId,
            articleId,
            lotNumber ?? $"LOT-{Guid.NewGuid():N}",
            12.345678m,
            unitId ?? KilogramId,
            createdAt ?? DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return lot;
    }

    private async Task AddMembershipAsync(Guid userId, Guid organizationId)
    {
        await using var context = database.CreateLotApiIdentityDbContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Create(
            userId,
            organizationId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateLotApiIdentityDbContext();
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
        await using var context = database.CreateLotApiIdentityDbContext();
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            userId,
            roleId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task DeactivateUserAsync(Guid userId)
    {
        await using var context = database.CreateLotApiIdentityDbContext();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        user.Deactivate(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
    }

    private async Task AssertInvalidQueryReturns400Async(string query)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            $"{LotCollectionPath(setup.Organization.OrganizationId)}?{query}",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
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

    private static async Task<LotListTestResponse> GetPageAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<LotListTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The lot list response body was empty.");
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

    private static string LotCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/lots";

    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        private int _readerCommandCount;

        public int ReaderCommandCount => Volatile.Read(ref _readerCommandCount);

        public void Reset()
        {
            Interlocked.Exchange(ref _readerCommandCount, 0);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommandCount);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId, Guid? LocationId);

    private sealed record AuthorizedSetup(
        TestAccount Account,
        TestOrganization Organization,
        Product Product,
        Article Article);

    private sealed record TokenResponse(string AccessToken);

    private sealed record LotListTestResponse(
        IReadOnlyList<LotTestResponse> Items,
        int Page,
        int PageSize,
        long TotalCount);

    private sealed record LotTestResponse(
        Guid Id,
        Guid OrganizationId,
        Guid ArticleId,
        string LotNumber,
        decimal Quantity,
        string UnitCode,
        DateTimeOffset CreatedAt);
}
