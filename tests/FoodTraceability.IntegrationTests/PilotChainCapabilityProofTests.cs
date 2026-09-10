// Pflichtkette aus dem Entwicklungsplan: OL-001 -> PRESS -> OIL-001 -> BOTTLE -> BOT-001.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.Articles;
using FoodTraceability.Api.Contracts.Authentication;
using FoodTraceability.Api.Contracts.Lots;
using FoodTraceability.Api.Contracts.Memberships;
using FoodTraceability.Api.Contracts.Organizations;
using FoodTraceability.Api.Contracts.Products;
using FoodTraceability.Api.Contracts.TraceabilityEvents;
using FoodTraceability.Api.Contracts.Traces;
using FoodTraceability.Api.Contracts.Users;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class PilotChainCapabilityProofTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";
    private const decimal OliveQuantity = 100m;
    private const decimal OilQuantity = 20m;
    private const decimal BottleQuantity = 100m;

    [Fact]
    public async Task RealApiRecordsPressAndBottleAsOneConnectedPilotChain()
    {
        await using var factory = CreateFactory();
        using var producerClient = factory.CreateClient();
        var (organization, location, oliveLot, oilLot, bottleLot, press, bottle) =
            await CreatePilotChainAsync(factory, producerClient);

        Assert.Equal(
            EventPath(organization.Id, press.Body.Id),
            press.Location.OriginalString);
        Assert.Equal(
            EventPath(organization.Id, bottle.Body.Id),
            bottle.Location.OriginalString);

        var readPress = await GetEventAsync(
            producerClient,
            press.Location,
            factory.RequestCancellationToken);
        var readBottle = await GetEventAsync(
            producerClient,
            bottle.Location,
            factory.RequestCancellationToken);

        Assert.Equal("PRESS", readPress.EventTypeCode);
        Assert.Equal("BOTTLE", readBottle.EventTypeCode);
        Assert.Equal(organization.Id, readPress.OrganizationId);
        Assert.Equal(organization.Id, readBottle.OrganizationId);
        Assert.Equal(location.Id, readPress.LocationId);
        Assert.Equal(location.Id, readBottle.LocationId);

        var pressHttpInput = Assert.Single(readPress.Inputs);
        var pressHttpOutput = Assert.Single(readPress.Outputs);
        var bottleHttpInput = Assert.Single(readBottle.Inputs);
        var bottleHttpOutput = Assert.Single(readBottle.Outputs);

        // Kernbehauptung: Der Output des PRESS und der Input des BOTTLE sind dasselbe Lot.
        Assert.Equal(pressHttpOutput.LotId, bottleHttpInput.LotId);

        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persistedLots = await context.Lots
            .AsNoTracking()
            .Where(lot => lot.OrganizationId == organization.Id)
            .ToDictionaryAsync(lot => lot.Id, factory.RequestCancellationToken);
        var persistedEvents = await context.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(traceabilityEvent => traceabilityEvent.Inputs)
            .Include(traceabilityEvent => traceabilityEvent.Outputs)
            .Where(traceabilityEvent => traceabilityEvent.OrganizationId == organization.Id)
            .ToListAsync(factory.RequestCancellationToken);

        Assert.Equal(3, persistedLots.Count);
        Assert.Equal(2, persistedEvents.Count);
        Assert.All(
            persistedEvents,
            traceabilityEvent => Assert.Equal(
                organization.Id,
                traceabilityEvent.OrganizationId));

        AssertHttpLine(
            pressHttpInput,
            oliveLot.Id,
            OliveQuantity,
            persistedLots[oliveLot.Id].UnitId);
        AssertHttpLine(
            pressHttpOutput,
            oilLot.Id,
            OilQuantity,
            persistedLots[oilLot.Id].UnitId);
        AssertHttpLine(
            bottleHttpInput,
            oilLot.Id,
            OilQuantity,
            persistedLots[oilLot.Id].UnitId);
        AssertHttpLine(
            bottleHttpOutput,
            bottleLot.Id,
            BottleQuantity,
            persistedLots[bottleLot.Id].UnitId);

        var persistedPress = Assert.Single(
            persistedEvents,
            traceabilityEvent => traceabilityEvent.Id == press.Body.Id);
        var persistedBottle = Assert.Single(
            persistedEvents,
            traceabilityEvent => traceabilityEvent.Id == bottle.Body.Id);
        var pressInput = Assert.Single(persistedPress.Inputs);
        var pressOutput = Assert.Single(persistedPress.Outputs);
        var bottleInput = Assert.Single(persistedBottle.Inputs);
        var bottleOutput = Assert.Single(persistedBottle.Outputs);

        AssertPersistedLine(
            pressInput.LotId,
            pressInput.Quantity,
            pressInput.UnitId,
            oliveLot.Id,
            OliveQuantity,
            persistedLots[oliveLot.Id].UnitId);
        AssertPersistedLine(
            pressOutput.LotId,
            pressOutput.Quantity,
            pressOutput.UnitId,
            oilLot.Id,
            OilQuantity,
            persistedLots[oilLot.Id].UnitId);
        AssertPersistedLine(
            bottleInput.LotId,
            bottleInput.Quantity,
            bottleInput.UnitId,
            oilLot.Id,
            OilQuantity,
            persistedLots[oilLot.Id].UnitId);
        AssertPersistedLine(
            bottleOutput.LotId,
            bottleOutput.Quantity,
            bottleOutput.UnitId,
            bottleLot.Id,
            BottleQuantity,
            persistedLots[bottleLot.Id].UnitId);

        // Dieselbe Verkettung muss auch in den persistierten Beziehungen bestehen.
        Assert.Equal(pressOutput.LotId, bottleInput.LotId);

        // D-32: lot.quantity bleibt die unveraenderte Initialmenge.
        Assert.Equal(OliveQuantity, persistedLots[oliveLot.Id].Quantity);
        Assert.Equal(OilQuantity, persistedLots[oilLot.Id].Quantity);
        Assert.Equal(BottleQuantity, persistedLots[bottleLot.Id].Quantity);
    }

    // EPIC 4, Pflichttest aus DEVELOPMENT_PLAN.md:
    // OL-001 → PRESS → OIL-001 → BOTTLE → BOT-001
    // Backward(BOT-001) enthält OIL-001 und OL-001. Forward(OL-001) enthält OIL-001 und BOT-001.
    [Fact]
    public async Task RealApiRecordsPilotChainAndTracesItBackwardAndForward()
    {
        await using var factory = CreateFactory();
        using var producerClient = factory.CreateClient();
        var (organization, _, oliveLot, oilLot, bottleLot, press, bottle) =
            await CreatePilotChainAsync(factory, producerClient);

        using var backwardResponse = await producerClient.GetAsync(
            $"{LotCollectionPath(organization.Id)}/{bottleLot.Id}/traceability/backward",
            factory.RequestCancellationToken);
        using var forwardResponse = await producerClient.GetAsync(
            $"{LotCollectionPath(organization.Id)}/{oliveLot.Id}/traceability/forward",
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, backwardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forwardResponse.StatusCode);
        var backward = await ReadRequiredAsync<TraceGraphResponse>(
            backwardResponse.Content, factory.RequestCancellationToken);
        var forward = await ReadRequiredAsync<TraceGraphResponse>(
            forwardResponse.Content, factory.RequestCancellationToken);

        TraceEdgeResponse[] expectedEdges =
        [
            new(press.Body.Id, "PRESS", press.Body.OccurredAt, oliveLot.Id, oilLot.Id),
            new(bottle.Body.Id, "BOTTLE", bottle.Body.OccurredAt, oilLot.Id, bottleLot.Id),
        ];

        // Evaluate all three mandatory statements even when one of them fails.
        Assert.Multiple(
            () =>
            {
                // Pflichtaussage 1: OL-001 → PRESS → OIL-001 → BOTTLE → BOT-001.
                Assert.Equal("OL-001", oliveLot.LotNumber);
                Assert.Equal("OIL-001", oilLot.LotNumber);
                Assert.Equal("BOT-001", bottleLot.LotNumber);
                Assert.Equal("PRESS", press.Body.EventTypeCode);
                Assert.Equal("BOTTLE", bottle.Body.EventTypeCode);
                Assert.Equal(oliveLot.Id, Assert.Single(press.Body.Inputs).LotId);
                Assert.Equal(oilLot.Id, Assert.Single(press.Body.Outputs).LotId);
                Assert.Equal(oilLot.Id, Assert.Single(bottle.Body.Inputs).LotId);
                Assert.Equal(bottleLot.Id, Assert.Single(bottle.Body.Outputs).LotId);
            },
            () =>
            {
                // Pflichtaussage 2: Backward(BOT-001) enthält OIL-001 und OL-001.
                Assert.Contains(backward.Nodes, node => node.LotId == oilLot.Id && node.LotNumber == "OIL-001");
                Assert.Contains(backward.Nodes, node => node.LotId == oliveLot.Id && node.LotNumber == "OL-001");
                Assert.Equal(bottleLot.Id, backward.RootLotId);
                Assert.Contains(backward.Nodes, node => node.LotId == bottleLot.Id && node.LotNumber == "BOT-001");
                Assert.Equal(3, backward.Nodes.Count);
                Assert.Equal(3, backward.Nodes.Select(node => node.LotId).Distinct().Count());
                Assert.Equal(expectedEdges.OrderBy(edge => edge.EventId), backward.Edges.OrderBy(edge => edge.EventId));
            },
            () =>
            {
                // Pflichtaussage 3: Forward(OL-001) enthält OIL-001 und BOT-001.
                Assert.Contains(forward.Nodes, node => node.LotId == oilLot.Id && node.LotNumber == "OIL-001");
                Assert.Contains(forward.Nodes, node => node.LotId == bottleLot.Id && node.LotNumber == "BOT-001");
                Assert.Equal(oliveLot.Id, forward.RootLotId);
                Assert.Contains(forward.Nodes, node => node.LotId == oliveLot.Id && node.LotNumber == "OL-001");
                Assert.Equal(3, forward.Nodes.Count);
                Assert.Equal(3, forward.Nodes.Select(node => node.LotId).Distinct().Count());
                Assert.Equal(expectedEdges.OrderBy(edge => edge.EventId), forward.Edges.OrderBy(edge => edge.EventId));
            },
            () =>
            {
                // Both graphs retain the same input → output direction, including event metadata.
                Assert.Equal(backward.Edges.OrderBy(edge => edge.EventId), forward.Edges.OrderBy(edge => edge.EventId));
            });
    }

    private async Task<(
        OrganizationResponse Organization,
        LocationResponse Location,
        LotResponse OliveLot,
        LotResponse OilLot,
        LotResponse BottleLot,
        (TraceabilityEventResponse Body, Uri Location) Press,
        (TraceabilityEventResponse Body, Uri Location) Bottle)> CreatePilotChainAsync(
            ApiWebApplicationFactory factory,
            HttpClient producerClient)
    {
        var administrator = await CreatePlatformAdministratorAsync();
        using var administratorClient = factory.CreateClient();
        await AuthenticateAsync(
            administratorClient,
            administrator.Email,
            administrator.Password,
            factory.RequestCancellationToken);

        var suffix = Guid.NewGuid().ToString("N");
        var organization = await PostAndReadCreatedAsync<OrganizationResponse>(
            administratorClient,
            "/api/v1/platform/organizations",
            new CreateOrganizationRequest(
                $"TRC-013a Pilot Organization {suffix}",
                VatId: null,
                TaxNumber: null,
                Email: null,
                Phone: null),
            factory.RequestCancellationToken);

        var memberEmail = $"trc-013a-producer-{suffix}@example.com";
        var member = await PostAndReadCreatedAsync<UserResponse>(
            administratorClient,
            "/api/v1/platform/users",
            new CreateUserRequest(
                memberEmail,
                "Pilot",
                "Producer",
                ValidPassword),
            factory.RequestCancellationToken);

        await PostExpectingCreatedAsync(
            administratorClient,
            MemberCollectionPath(organization.Id),
            new AddMemberRequest(member.Id),
            factory.RequestCancellationToken);
        await PostExpectingCreatedAsync(
            administratorClient,
            RoleCollectionPath(organization.Id, member.Id),
            new AssignRoleRequest(StandardRoleIds.OrganizationAdmin),
            factory.RequestCancellationToken);
        await PostExpectingCreatedAsync(
            administratorClient,
            RoleCollectionPath(organization.Id, member.Id),
            new AssignRoleRequest(StandardRoleIds.Producer),
            factory.RequestCancellationToken);

        await AuthenticateAsync(
            producerClient,
            memberEmail,
            ValidPassword,
            factory.RequestCancellationToken);

        var location = await PostAndReadCreatedAsync<LocationResponse>(
            producerClient,
            LocationCollectionPath(organization.Id),
            new CreateLocationRequest(
                "Pilot Olive Mill",
                "Kalamata",
                "Peloponnese",
                "GR",
                Latitude: null,
                Longitude: null),
            factory.RequestCancellationToken);

        var olivesProduct = await CreateProductAsync(
            administratorClient,
            $"TRC013A-OLIVES-{suffix}",
            "Olives",
            factory.RequestCancellationToken);
        var oilProduct = await CreateProductAsync(
            administratorClient,
            $"TRC013A-OIL-{suffix}",
            "Olive Oil",
            factory.RequestCancellationToken);
        var bottlesProduct = await CreateProductAsync(
            administratorClient,
            $"TRC013A-BOTTLES-{suffix}",
            "Bottled Olive Oil",
            factory.RequestCancellationToken);

        var olivesArticle = await CreateArticleAsync(
            producerClient,
            organization.Id,
            olivesProduct.Id,
            "OLIVES-001",
            factory.RequestCancellationToken);
        var oilArticle = await CreateArticleAsync(
            producerClient,
            organization.Id,
            oilProduct.Id,
            "OIL-001",
            factory.RequestCancellationToken);
        var bottlesArticle = await CreateArticleAsync(
            producerClient,
            organization.Id,
            bottlesProduct.Id,
            "BOTTLES-001",
            factory.RequestCancellationToken);

        var oliveLot = await CreateLotAsync(
            producerClient,
            organization.Id,
            olivesArticle.Id,
            "OL-001",
            OliveQuantity,
            "KG",
            factory.RequestCancellationToken);
        var oilLot = await CreateLotAsync(
            producerClient,
            organization.Id,
            oilArticle.Id,
            "OIL-001",
            OilQuantity,
            "L",
            factory.RequestCancellationToken);
        var bottleLot = await CreateLotAsync(
            producerClient,
            organization.Id,
            bottlesArticle.Id,
            "BOT-001",
            BottleQuantity,
            "PCS",
            factory.RequestCancellationToken);

        var press = await CreateEventAsync(
            producerClient,
            organization.Id,
            new CreateTraceabilityEventRequest(
                "PRESS",
                location.Id,
                DateTimeOffset.UtcNow,
                $"TRC-013a-PRESS-{suffix}",
                "Pilot chain capability proof: olives to oil",
                [new TraceabilityEventLotRequest(oliveLot.Id, OliveQuantity)],
                [new TraceabilityEventLotRequest(oilLot.Id, OilQuantity)]),
            factory.RequestCancellationToken);
        var bottle = await CreateEventAsync(
            producerClient,
            organization.Id,
            new CreateTraceabilityEventRequest(
                "BOTTLE",
                location.Id,
                DateTimeOffset.UtcNow,
                $"TRC-013a-BOTTLE-{suffix}",
                "Pilot chain capability proof: oil to bottles",
                [new TraceabilityEventLotRequest(oilLot.Id, OilQuantity)],
                [new TraceabilityEventLotRequest(bottleLot.Id, BottleQuantity)]),
            factory.RequestCancellationToken);

        return (organization, location, oliveLot, oilLot, bottleLot, press, bottle);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<TestAccount> CreatePlatformAdministratorAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"trc-013a-admin-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Pilot",
            "Administrator",
            now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword),
            now);

        await using var context = database.CreateLotApiIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            userId,
            StandardRoleIds.PlatformAdmin,
            now));
        await context.SaveChangesAsync();

        return new TestAccount(email, ValidPassword);
    }

    private static Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        string productCode,
        string name,
        CancellationToken cancellationToken) =>
        PostAndReadCreatedAsync<ProductResponse>(
            client,
            "/api/v1/platform/products",
            new CreateProductRequest(productCode, name),
            cancellationToken);

    private static Task<ArticleResponse> CreateArticleAsync(
        HttpClient client,
        Guid organizationId,
        Guid productId,
        string articleNumber,
        CancellationToken cancellationToken) =>
        PostAndReadCreatedAsync<ArticleResponse>(
            client,
            ArticleCollectionPath(organizationId),
            new CreateArticleRequest(productId, articleNumber, Gtin: null),
            cancellationToken);

    private static Task<LotResponse> CreateLotAsync(
        HttpClient client,
        Guid organizationId,
        Guid articleId,
        string lotNumber,
        decimal quantity,
        string unitCode,
        CancellationToken cancellationToken) =>
        PostAndReadCreatedAsync<LotResponse>(
            client,
            LotCollectionPath(organizationId),
            new CreateLotRequest(articleId, lotNumber, quantity, unitCode),
            cancellationToken);

    private static async Task<(TraceabilityEventResponse Body, Uri Location)> CreateEventAsync(
        HttpClient client,
        Guid organizationId,
        CreateTraceabilityEventRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(organizationId),
            request,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location
            ?? throw new InvalidOperationException(
                "The traceability event response had no Location header.");
        var body = await ReadRequiredAsync<TraceabilityEventResponse>(
            response.Content,
            cancellationToken);
        return (body, location);
    }

    private static async Task<TraceabilityEventResponse> GetEventAsync(
        HttpClient client,
        Uri location,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(location, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadRequiredAsync<TraceabilityEventResponse>(
            response.Content,
            cancellationToken);
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Email = email, Password = password },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await ReadRequiredAsync<AuthenticationTokenResponse>(
            response.Content,
            cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);
    }

    private static async Task PostExpectingCreatedAsync(
        HttpClient client,
        string requestUri,
        object request,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            requestUri,
            request,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<TResponse> PostAndReadCreatedAsync<TResponse>(
        HttpClient client,
        string requestUri,
        object request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var response = await client.PostAsJsonAsync(
            requestUri,
            request,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadRequiredAsync<TResponse>(response.Content, cancellationToken);
    }

    private static async Task<TResponse> ReadRequiredAsync<TResponse>(
        HttpContent content,
        CancellationToken cancellationToken)
        where TResponse : class =>
        await content.ReadFromJsonAsync<TResponse>(cancellationToken)
        ?? throw new InvalidOperationException(
            $"The {typeof(TResponse).Name} response body was empty.");

    private static void AssertHttpLine(
        TraceabilityEventLotResponse line,
        Guid expectedLotId,
        decimal expectedQuantity,
        Guid expectedUnitId)
    {
        Assert.Equal(expectedLotId, line.LotId);
        Assert.Equal(expectedQuantity, line.Quantity);
        Assert.Equal(expectedUnitId, line.UnitId);
    }

    private static void AssertPersistedLine(
        Guid actualLotId,
        decimal actualQuantity,
        Guid actualUnitId,
        Guid expectedLotId,
        decimal expectedQuantity,
        Guid expectedUnitId)
    {
        Assert.Equal(expectedLotId, actualLotId);
        Assert.Equal(expectedQuantity, actualQuantity);
        Assert.Equal(expectedUnitId, actualUnitId);
    }

    private static string MemberCollectionPath(Guid organizationId) =>
        $"/api/v1/platform/organizations/{organizationId}/members";

    private static string RoleCollectionPath(Guid organizationId, Guid userId) =>
        $"{MemberCollectionPath(organizationId)}/{userId}/roles";

    private static string LocationCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/locations";

    private static string ArticleCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/articles";

    private static string LotCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/lots";

    private static string EventCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/traceability/events";

    private static string EventPath(Guid organizationId, Guid eventId) =>
        $"{EventCollectionPath(organizationId)}/{eventId}";

    private sealed record TestAccount(string Email, string Password);
}
