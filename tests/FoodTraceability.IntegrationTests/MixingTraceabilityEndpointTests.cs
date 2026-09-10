// TRC-014: OL-A + OL-B + OL-C --MIX--> OL-MIX.
// Proves persisted quantities/units, backward convergence, forward sibling exclusion,
// and independent consumption limits for every input lot through the real APIs.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FoodTraceability.Api.Contracts.Lots;
using FoodTraceability.Api.Contracts.TraceabilityEvents;
using FoodTraceability.Api.Contracts.Traces;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class MixingTraceabilityEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task MixWithThreeInputsAndOneOutputIsCreatedAndLocationReturnsQuantitiesAndUnits()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateMixingLotsAsync(client, setup, cancellationToken);

        using var response = await client.PostAsJsonAsync(EventPath(setup.Organization.Id),
            MixRequest(setup, lots), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken);
        Assert.NotNull(created);
        using var getResponse = await client.GetAsync(response.Headers.Location, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var persisted = await getResponse.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(created.Id, persisted.Id);

        await using var catalog = database.CreateLotApiCatalogDbContext();
        var kilogramId = await catalog.Units
            .Where(unit => unit.Code == UnitCode.Create("KG"))
            .Select(unit => unit.Id).SingleAsync(cancellationToken);
        foreach (var body in new[] { created, persisted })
        {
            Assert.Equal("MIX", body.EventTypeCode);
            Assert.Equal(setup.Organization.Id, body.OrganizationId);
            Assert.Equal(3, body.Inputs.Count);
            Assert.Equal(new TraceabilityEventLotResponse(lots.A.Id, 20m, kilogramId),
                Assert.Single(body.Inputs, input => input.LotId == lots.A.Id));
            Assert.Equal(new TraceabilityEventLotResponse(lots.B.Id, 30m, kilogramId),
                Assert.Single(body.Inputs, input => input.LotId == lots.B.Id));
            Assert.Equal(new TraceabilityEventLotResponse(lots.C.Id, 40m, kilogramId),
                Assert.Single(body.Inputs, input => input.LotId == lots.C.Id));
            Assert.Equal(new TraceabilityEventLotResponse(lots.Mixed.Id, 90m, kilogramId),
                Assert.Single(body.Outputs));
        }
    }

    [Fact]
    public async Task BackwardMixedLotContainsExactlyThreeSourcesAndRootWithThreeDirectedMixEdges()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateMixingLotsAsync(client, setup, cancellationToken);
        var mix = await CreateMixAsync(client, setup, lots, cancellationToken);

        var graph = await ReadGraphAsync(client, setup.Organization.Id, lots.Mixed.Id, "backward", cancellationToken);

        Assert.Equal(lots.Mixed.Id, graph.RootLotId);
        // Report both missing nodes and missing edges in the two-input negative proof.
        Assert.Multiple(
            () => Assert.Equal(4, graph.Nodes.Count),
            () => Assert.Equal(3, graph.Edges.Count),
            () => AssertNode(graph, lots.A),
            () => AssertNode(graph, lots.B),
            () => AssertNode(graph, lots.C),
            () => AssertNode(graph, lots.Mixed));
        foreach (var source in lots.Sources)
        {
            Assert.Equal(new TraceEdgeResponse(mix.Id, "MIX", mix.OccurredAt, source.Id, lots.Mixed.Id),
                Assert.Single(graph.Edges, edge => edge.FromLotId == source.Id));
        }
        AssertUniqueGraph(graph);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ForwardEachSourceContainsOnlyItselfAndMixedLotWithoutSiblings(int sourceIndex)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateMixingLotsAsync(client, setup, cancellationToken);
        var mix = await CreateMixAsync(client, setup, lots, cancellationToken);
        var source = lots.Sources[sourceIndex];

        var graph = await ReadGraphAsync(client, setup.Organization.Id, source.Id, "forward", cancellationToken);

        Assert.Equal(source.Id, graph.RootLotId);
        Assert.Equal(2, graph.Nodes.Count);
        AssertNode(graph, source);
        AssertNode(graph, lots.Mixed);
        foreach (var sibling in lots.Sources.Where(lot => lot.Id != source.Id))
        {
            Assert.DoesNotContain(graph.Nodes, node => node.LotId == sibling.Id);
        }
        Assert.Equal(new TraceEdgeResponse(mix.Id, "MIX", mix.OccurredAt, source.Id, lots.Mixed.Id),
            Assert.Single(graph.Edges));
        AssertUniqueGraph(graph);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SecondMixRejectsOverconsumptionAtEveryInputPosition(int overconsumedIndex)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateMixingLotsAsync(client, setup, cancellationToken);
        await CreateMixAsync(client, setup, lots, cancellationToken);

        // Initial quantities are 100 KG each; the first MIX consumes 20/30/40 KG.
        // Only the selected position exceeds its remainder, by exactly 1 KG.
        decimal[] remaining = [80m, 70m, 60m];
        var inputs = lots.Sources.Select((lot, index) => new TraceabilityEventLotRequest(
            lot.Id, index == overconsumedIndex ? remaining[index] + 1m : 1m)).ToArray();
        var output = await CreateLotAsync(client, setup, "OL-MIX-SECOND",
            inputs.Sum(input => input.Quantity), cancellationToken);
        var request = new CreateTraceabilityEventRequest("MIX", setup.Organization.LocationId,
            new DateTimeOffset(2026, 9, 9, 11, 0, 0, TimeSpan.Zero), null, null,
            inputs, [new(output.Id, output.Quantity)]);

        using var response = await client.PostAsJsonAsync(EventPath(setup.Organization.Id), request, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
        Assert.Equal("TRACEABILITY_EVENT_CONFLICT", problem["errorCode"]!.GetValue<string>());
        Assert.Contains(lots.Sources[overconsumedIndex].Id.ToString(),
            problem["detail"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private ApiWebApplicationFactory CreateFactory() => new(new Dictionary<string, string?>
    {
        ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
        ["RateLimiting:Authentication:PermitLimit"] = "100",
    });

    private static async Task<MixingLots> CreateMixingLotsAsync(
        HttpClient client, AuthorizedSetup setup, CancellationToken cancellationToken)
    {
        var a = await CreateLotAsync(client, setup, "OL-A", 100m, cancellationToken);
        var b = await CreateLotAsync(client, setup, "OL-B", 100m, cancellationToken);
        var c = await CreateLotAsync(client, setup, "OL-C", 100m, cancellationToken);
        var mixed = await CreateLotAsync(client, setup, "OL-MIX", 90m, cancellationToken);
        return new MixingLots(a, b, c, mixed);
    }

    private static async Task<LotResponse> CreateLotAsync(
        HttpClient client, AuthorizedSetup setup, string lotNumber, decimal quantity,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{setup.Organization.Id}/lots",
            new CreateLotRequest(setup.Article.Id, lotNumber, quantity, "KG"), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lot = await response.Content.ReadFromJsonAsync<LotResponse>(cancellationToken);
        Assert.NotNull(lot);
        return lot;
    }

    private static CreateTraceabilityEventRequest MixRequest(AuthorizedSetup setup, MixingLots lots) =>
        new("MIX", setup.Organization.LocationId,
            new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero), null, null,
            [new(lots.A.Id, 20m), new(lots.B.Id, 30m), new(lots.C.Id, 40m)],
            [new(lots.Mixed.Id, 90m)]);

    private static async Task<TraceabilityEventResponse> CreateMixAsync(
        HttpClient client, AuthorizedSetup setup, MixingLots lots, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            EventPath(setup.Organization.Id), MixRequest(setup, lots), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mix = await response.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken);
        Assert.NotNull(mix);
        return mix;
    }

    private static async Task<TraceGraphResponse> ReadGraphAsync(
        HttpClient client, Guid organizationId, Guid lotId, string direction, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{organizationId}/lots/{lotId}/traceability/{direction}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var graph = await response.Content.ReadFromJsonAsync<TraceGraphResponse>(cancellationToken);
        Assert.NotNull(graph);
        return graph;
    }

    private static void AssertNode(TraceGraphResponse graph, LotResponse lot) =>
        Assert.Equal(new TraceNodeResponse(lot.Id, lot.LotNumber, lot.ArticleId, lot.Quantity, lot.UnitCode),
            Assert.Single(graph.Nodes, node => node.LotId == lot.Id));

    private static void AssertUniqueGraph(TraceGraphResponse graph)
    {
        Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(node => node.LotId).Distinct().Count());
        Assert.Equal(graph.Edges.Count, graph.Edges.Select(edge => (edge.EventId, edge.FromLotId, edge.ToLotId)).Distinct().Count());
    }

    private static string EventPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/traceability/events";

    private sealed record MixingLots(LotResponse A, LotResponse B, LotResponse C, LotResponse Mixed)
    {
        public LotResponse[] Sources => [A, B, C];
    }

    private async Task<AuthorizedSetup> CreateAuthorizedSetupAsync(Guid roleId)
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var article = await CreateArticleAsync(organization.Id, product.Id);
        await AddMembershipAndOrganizationRoleAsync(account.UserId, organization.Id, roleId);
        return new AuthorizedSetup(account, organization, product, article);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"mixing-api-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Event", "Test", now);
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

    private async Task<TestOrganization> CreateOrganizationAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        await using var context = database.CreateLotApiOrganizationsDbContext();
        context.Organizations.Add(Organization.Create(
            organizationId,
            $"Event API Organization {organizationId:N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            now));
        context.Locations.Add(Location.Create(
            locationId,
            organizationId,
            "Event API Location",
            city: null,
            region: null,
            countryCode: null,
            latitude: null,
            longitude: null,
            now));
        await context.SaveChangesAsync();
        return new TestOrganization(organizationId, locationId);
    }

    private async Task<Product> CreateProductAsync()
    {
        var id = Guid.NewGuid();
        var product = Product.Create(
            id,
            $"EVENT-PRODUCT-{id:N}",
            $"Event API Product {id:N}",
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
            $"EVENT-SKU-{id:N}",
            gtin: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiCatalogDbContext();
        context.Articles.Add(article);
        await context.SaveChangesAsync();
        return article;
    }

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId)
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
            locationId: null,
            now));
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
        var tokens = await response.Content.ReadFromJsonAsync<FoodTraceability.Api.Contracts.Authentication.AuthenticationTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);
    }

    private sealed record TestAccount(Guid UserId, string Email);
    private sealed record TestOrganization(Guid Id, Guid LocationId);
    private sealed record AuthorizedSetup(
        TestAccount Account, TestOrganization Organization, Product Product, Article Article);
}
