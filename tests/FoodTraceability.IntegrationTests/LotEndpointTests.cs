using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LotEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";
    private const string ValidLotNumber = "LOT-VALID-001";
    private const string KilogramCode = "KG";

    private static readonly Guid KilogramId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    [Fact]
    public async Task ProducerWithLotCreateCreatesLotInRouteOrganization()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/v1/organizations/{setup.Organization.OrganizationId}/lots/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(setup.Organization.OrganizationId, body.OrganizationId);
        Assert.Equal(setup.Article.Id, body.ArticleId);
        Assert.Equal(KilogramCode, body.UnitCode);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persisted = await context.Lots
            .AsNoTracking()
            .SingleAsync(lot => lot.Id == body.Id);
        Assert.Equal(setup.Organization.OrganizationId, persisted.OrganizationId);
        Assert.Equal(setup.Article.Id, persisted.ArticleId);
        Assert.Equal(ValidLotNumber, persisted.LotNumber);
        Assert.Equal(12.345678m, persisted.Quantity);
        Assert.Equal(KilogramId, persisted.UnitId);
    }

    [Fact]
    public async Task MemberWithLotReadReadsOwnLot()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(setup.Lot.Id, body.Id);
        Assert.Equal(setup.Organization.OrganizationId, body.OrganizationId);
        Assert.Equal(setup.Article.Id, body.ArticleId);
        Assert.Equal(setup.Lot.LotNumber, body.LotNumber);
        Assert.Equal(setup.Lot.Quantity, body.Quantity);
        Assert.Equal(KilogramCode, body.UnitCode);
    }

    [Fact]
    public async Task UnauthenticatedCreateReturns401()
    {
        var setup = await CreateDataSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
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
        var setup = await CreateDataSetupAsync(createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
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
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
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
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await DeactivateUserAsync(setup.Account.UserId);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
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
        var setup = await CreateDataSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ReadWithoutMembershipReturns403()
    {
        var account = await CreateAccountAsync();
        var setup = await CreateDataSetupAsync(createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutLotCreateReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.QualityManager);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipWithoutLotReadReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.OrganizationAdmin,
            createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedLotCreateOnlyReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLocation: true,
            locationScoped: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task LocationScopedLotReadOnlyReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLot: true,
            createLocation: true,
            locationScoped: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformRoleWithoutMembershipCannotCreate()
    {
        var account = await CreateAccountAsync();
        var setup = await CreateDataSetupAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformRoleWithoutMembershipCannotRead()
    {
        var account = await CreateAccountAsync();
        var setup = await CreateDataSetupAsync(createLot: true);
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
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
            LotCollectionPath(Guid.NewGuid()),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ReadForUnknownOrganizationReturns403InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.Producer,
            createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            LotPath(Guid.NewGuid(), setup.Lot!.Id),
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
        var articleB = await CreateArticleAsync(organizationB.OrganizationId, product.Id);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organizationA.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        var totalBefore = await CountLotsAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(organizationB.OrganizationId),
            ValidRequest(articleB.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(response, factory.RequestCancellationToken);
        Assert.Equal(totalBefore, await CountLotsAsync());
        Assert.Equal(0, await CountLotsAsync(organizationB.OrganizationId));
    }

    [Fact]
    public async Task OrganizationAdminCannotCreateOrReadLots()
    {
        var setup = await CreateAuthorizedSetupAsync(
            StandardRoleIds.OrganizationAdmin,
            createLot: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);
        using var readResponse = await client.GetAsync(
            LotPath(setup.Organization.OrganizationId, setup.Lot!.Id),
            factory.RequestCancellationToken);

        await AssertForbiddenAsync(createResponse, factory.RequestCancellationToken);
        await AssertForbiddenAsync(readResponse, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task BodyOrganizationIdCannotOverrideRouteOrganizationScope()
    {
        var account = await CreateAccountAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var injectedOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var routeArticle = await CreateArticleAsync(routeOrganization.OrganizationId, product.Id);
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
            ["articleId"] = routeArticle.Id,
            ["lotNumber"] = UniqueLotNumber(),
            ["quantity"] = 10.25m,
            ["unitCode"] = KilogramCode,
            ["organizationId"] = injectedOrganization.OrganizationId,
        };

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(routeOrganization.OrganizationId),
            request,
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(routeOrganization.OrganizationId, body.OrganizationId);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persisted = await context.Lots
            .AsNoTracking()
            .SingleAsync(lot => lot.Id == body.Id);
        Assert.Equal(routeOrganization.OrganizationId, persisted.OrganizationId);
        Assert.False(await context.Lots.AnyAsync(
            lot => lot.OrganizationId == injectedOrganization.OrganizationId));
    }

    [Fact]
    public async Task UnknownArticleIdReturns400InsteadOf404()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "LOT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ForeignAndUnknownArticlesReturnIndistinguishable400Responses()
    {
        var account = await CreateAccountAsync();
        var ownOrganization = await CreateOrganizationAsync();
        var foreignOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var foreignArticle = await CreateArticleAsync(foreignOrganization.OrganizationId, product.Id);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            ownOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var foreignResponse = await client.PostAsJsonAsync(
            LotCollectionPath(ownOrganization.OrganizationId),
            ValidRequest(foreignArticle.Id),
            factory.RequestCancellationToken);
        using var missingResponse = await client.PostAsJsonAsync(
            LotCollectionPath(ownOrganization.OrganizationId),
            ValidRequest(Guid.NewGuid()),
            factory.RequestCancellationToken);
        var foreignBody = RemoveCorrelationIdentifiers(
            await foreignResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var missingBody = RemoveCorrelationIdentifiers(
            await missingResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal(foreignBody, missingBody);
    }

    [Fact]
    public async Task UnknownUnitCodeReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { UnitCode = "XX" },
            factory.RequestCancellationToken);

        using var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "LOT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
        Assert.Equal(
            "The referenced unit does not exist.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DuplicateLotNumberInSameOrganizationReturns409()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lotNumber = UniqueLotNumber();
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            lotNumber);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { LotNumber = lotNumber },
            factory.RequestCancellationToken);

        await AssertConflictAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateLotNumberWithDifferentCasingReturns409()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lotNumber = $"LOT-{Guid.NewGuid():N}";
        await CreateLotAsync(
            setup.Organization.OrganizationId,
            setup.Article.Id,
            lotNumber);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { LotNumber = lotNumber.ToLowerInvariant() },
            factory.RequestCancellationToken);

        await AssertConflictAsync(response, factory.RequestCancellationToken);
    }

    [Fact]
    public async Task SameLotNumberInAnotherOrganizationCreatesLot()
    {
        var account = await CreateAccountAsync();
        var existingOrganization = await CreateOrganizationAsync();
        var routeOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var existingArticle = await CreateArticleAsync(existingOrganization.OrganizationId, product.Id);
        var routeArticle = await CreateArticleAsync(routeOrganization.OrganizationId, product.Id);
        var lotNumber = UniqueLotNumber();
        await CreateLotAsync(existingOrganization.OrganizationId, existingArticle.Id, lotNumber);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            routeOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(routeOrganization.OrganizationId),
            ValidRequest(routeArticle.Id) with { LotNumber = lotNumber },
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task LotNumberIsTrimmedAndOriginalCasingIsPreserved()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lotNumber = $"MiXeD-{Guid.NewGuid():N}";
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { LotNumber = $"  {lotNumber}  " },
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(lotNumber, body.LotNumber);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persisted = await context.Lots
            .AsNoTracking()
            .SingleAsync(lot => lot.Id == body.Id);
        Assert.Equal(lotNumber, persisted.LotNumber);
    }

    [Fact]
    public async Task QuantityWithSevenSignificantDecimalPlacesReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { Quantity = 1.0000005m },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "LOT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task QuantityWithSixDecimalPlacesIsCreatedAndStoredExactly()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        const decimal quantity = 987654.123456m;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { Quantity = quantity },
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(quantity, body.Quantity);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persistedQuantity = await context.Lots
            .AsNoTracking()
            .Where(lot => lot.Id == body.Id)
            .Select(lot => lot.Quantity)
            .SingleAsync();
        Assert.Equal(quantity, persistedQuantity);
    }

    [Fact]
    public async Task ZeroQuantityReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { Quantity = 0m },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "LOT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task QuantityOutsideNumericRangeReturns400InsteadOf500()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { Quantity = 1000000000000m },
            factory.RequestCancellationToken);

        using var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "LOT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
        Assert.Equal(
            "Quantity exceeds the supported range.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task LowercaseUnitCodeIsAcceptedAndResponseIsNormalized()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id) with { UnitCode = "kg" },
            factory.RequestCancellationToken);
        var body = await ReadLotAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(KilogramCode, body.UnitCode);
    }

    [Fact]
    public async Task ForeignAndMissingLotIdsReturnIndistinguishable404Responses()
    {
        var account = await CreateAccountAsync();
        var ownOrganization = await CreateOrganizationAsync();
        var foreignOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var foreignArticle = await CreateArticleAsync(foreignOrganization.OrganizationId, product.Id);
        var foreignLot = await CreateLotAsync(
            foreignOrganization.OrganizationId,
            foreignArticle.Id);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            ownOrganization.OrganizationId,
            StandardRoleIds.Producer,
            locationId: null);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var foreignResponse = await client.GetAsync(
            LotPath(ownOrganization.OrganizationId, foreignLot.Id),
            factory.RequestCancellationToken);
        using var missingResponse = await client.GetAsync(
            LotPath(ownOrganization.OrganizationId, Guid.NewGuid()),
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
    public async Task ResponseContainsUnitCodeAndDoesNotExposeUnitId()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            LotCollectionPath(setup.Organization.OrganizationId),
            ValidRequest(setup.Article.Id),
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            KilogramCode,
            document.RootElement.GetProperty("unitCode").GetString());
        Assert.False(document.RootElement.TryGetProperty("unitId", out _));
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<AuthorizedSetup> CreateAuthorizedSetupAsync(
        Guid roleId,
        bool createLot = false,
        bool createLocation = false,
        bool locationScoped = false)
    {
        var account = await CreateAccountAsync();
        var setup = await CreateDataSetupAsync(createLot, createLocation);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            setup.Organization.OrganizationId,
            roleId,
            locationScoped ? setup.Organization.LocationId : null);
        return new AuthorizedSetup(
            account,
            setup.Organization,
            setup.Product,
            setup.Article,
            setup.Lot);
    }

    private async Task<DataSetup> CreateDataSetupAsync(
        bool createLot = false,
        bool createLocation = false)
    {
        var organization = await CreateOrganizationAsync(createLocation);
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.OrganizationId, product.Id);
        var lot = createLot
            ? await CreateLotAsync(organization.OrganizationId, article.Id)
            : null;
        return new DataSetup(organization, product, article, lot);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"lot-api-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Lot", "Test", now);
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

    private async Task<TestOrganization> CreateOrganizationAsync(
        bool createLocation = false)
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = createLocation ? Guid.NewGuid() : (Guid?)null;
        await using var context = database.CreateLotApiOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Lot API Organization {organizationId:N}",
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
                "Lot Authorization Location",
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
            $"Lot API Product {id:N}",
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task<Article> CreateArticleAsync(
        Guid organizationId,
        Guid productId)
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
        decimal quantity = 12.345678m)
    {
        var lot = Lot.Create(
            Guid.NewGuid(),
            organizationId,
            articleId,
            lotNumber ?? UniqueLotNumber(),
            quantity,
            KilogramId,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return lot;
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

    private async Task<int> CountLotsAsync(Guid? organizationId = null)
    {
        await using var context = database.CreateLotApiTraceabilityDbContext();
        return organizationId is null
            ? await context.Lots.CountAsync()
            : await context.Lots.CountAsync(
                lot => lot.OrganizationId == organizationId.Value);
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

    private static async Task<LotTestResponse> ReadLotAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<LotTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The lot response body was empty.");
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
        CancellationToken cancellationToken)
    {
        using var document = await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "LOT_CONFLICT",
            cancellationToken);
        Assert.Equal(
            "A lot with the same lot number already exists in this organization.",
            document.RootElement.GetProperty("detail").GetString());
    }

    private static async Task<JsonDocument> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedErrorCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(
            expectedErrorCode,
            document.RootElement.GetProperty("errorCode").GetString());
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

    private static CreateLotTestRequest ValidRequest(Guid articleId) =>
        new(articleId, ValidLotNumber, 12.345678m, KilogramCode);

    private static string UniqueLotNumber() => $"LOT-{Guid.NewGuid():N}";

    private static string LotCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/lots";

    private static string LotPath(Guid organizationId, Guid lotId) =>
        $"{LotCollectionPath(organizationId)}/{lotId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid OrganizationId, Guid? LocationId);

    private sealed record DataSetup(
        TestOrganization Organization,
        Product Product,
        Article Article,
        Lot? Lot);

    private sealed record AuthorizedSetup(
        TestAccount Account,
        TestOrganization Organization,
        Product Product,
        Article Article,
        Lot? Lot);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateLotTestRequest(
        Guid ArticleId,
        string LotNumber,
        decimal Quantity,
        string UnitCode);

    private sealed record LotTestResponse(
        Guid Id,
        Guid OrganizationId,
        Guid ArticleId,
        string LotNumber,
        decimal Quantity,
        string UnitCode,
        DateTimeOffset CreatedAt);
}
