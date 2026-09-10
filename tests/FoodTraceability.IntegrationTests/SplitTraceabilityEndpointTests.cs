// TRC-015: OL-SRC --SPLIT--> OL-X + OL-Y + OL-Z (90 of 100 KG; 10 KG remain).
// Proves persisted quantities/units, forward branching, backward sibling exclusion,
// exact remaining consumption, and unchanged source production quantity through the real APIs.
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
public sealed class SplitTraceabilityEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task SplitWithOneInputAndThreeOutputsIsCreatedAndLocationReturnsQuantitiesAndUnits()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateSplitLotsAsync(client, setup, cancellationToken);

        using var response = await client.PostAsJsonAsync(EventPath(setup.Organization.Id),
            SplitRequest(setup, lots), cancellationToken);

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
            Assert.Equal("SPLIT", body.EventTypeCode);
            Assert.Equal(setup.Organization.Id, body.OrganizationId);
            Assert.Equal(new TraceabilityEventLotResponse(lots.Source.Id, 90m, kilogramId),
                Assert.Single(body.Inputs));
            Assert.Equal(3, body.Outputs.Count);
            Assert.Equal(new TraceabilityEventLotResponse(lots.X.Id, 20m, kilogramId),
                Assert.Single(body.Outputs, output => output.LotId == lots.X.Id));
            Assert.Equal(new TraceabilityEventLotResponse(lots.Y.Id, 30m, kilogramId),
                Assert.Single(body.Outputs, output => output.LotId == lots.Y.Id));
            Assert.Equal(new TraceabilityEventLotResponse(lots.Z.Id, 40m, kilogramId),
                Assert.Single(body.Outputs, output => output.LotId == lots.Z.Id));
        }
    }

    [Fact]
    public async Task ForwardSourceContainsExactlyThreeOutputsAndRootWithThreeDirectedSplitEdges()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateSplitLotsAsync(client, setup, cancellationToken);
        var split = await CreateSplitAsync(client, setup, lots, cancellationToken);

        var graph = await ReadGraphAsync(client, setup.Organization.Id, lots.Source.Id, "forward", cancellationToken);

        Assert.Equal(lots.Source.Id, graph.RootLotId);
        // Report both missing nodes and missing edges in the two-output negative proof.
        Assert.Multiple(
            () => Assert.Equal(4, graph.Nodes.Count),
            () => Assert.Equal(3, graph.Edges.Count),
            () => AssertNode(graph, lots.Source),
            () => AssertNode(graph, lots.X),
            () => AssertNode(graph, lots.Y),
            () => AssertNode(graph, lots.Z));
        foreach (var output in lots.Outputs)
        {
            Assert.Equal(new TraceEdgeResponse(split.Id, "SPLIT", split.OccurredAt, lots.Source.Id, output.Id),
                Assert.Single(graph.Edges, edge => edge.ToLotId == output.Id));
        }
        AssertUniqueGraph(graph);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BackwardEachOutputContainsOnlyItselfAndSourceWithoutSiblings(int outputIndex)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateSplitLotsAsync(client, setup, cancellationToken);
        var split = await CreateSplitAsync(client, setup, lots, cancellationToken);
        var output = lots.Outputs[outputIndex];

        var graph = await ReadGraphAsync(client, setup.Organization.Id, output.Id, "backward", cancellationToken);

        Assert.Equal(output.Id, graph.RootLotId);
        Assert.Equal(2, graph.Nodes.Count);
        AssertNode(graph, output);
        AssertNode(graph, lots.Source);
        foreach (var sibling in lots.Outputs.Where(lot => lot.Id != output.Id))
        {
            Assert.DoesNotContain(graph.Nodes, node => node.LotId == sibling.Id);
        }
        Assert.Equal(new TraceEdgeResponse(split.Id, "SPLIT", split.OccurredAt, lots.Source.Id, output.Id),
            Assert.Single(graph.Edges));
        AssertUniqueGraph(graph);
    }

    [Theory]
    [InlineData(10, HttpStatusCode.Created)]
    [InlineData(11, HttpStatusCode.Conflict)]
    public async Task SecondEventEnforcesExactRemainderAndSourceQuantityStaysUnchanged(
        int consumedQuantity, HttpStatusCode expectedStatus)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var lots = await CreateSplitLotsAsync(client, setup, cancellationToken);
        await CreateSplitAsync(client, setup, lots, cancellationToken);

        // D-32: quantity is the 100 KG production quantity, not the 10 KG remainder.
        await AssertSourceQuantityUnchangedAsync(client, setup, lots.Source, cancellationToken);

        // The first SPLIT consumes 90 KG. Independently test the exact 10 KG remainder
        // and overconsumption by 1 KG, each against a fresh source and first event.
        var output = await CreateLotAsync(client, setup, "OL-SECOND", consumedQuantity, cancellationToken);
        var request = new CreateTraceabilityEventRequest("SPLIT", setup.Organization.LocationId,
            new DateTimeOffset(2026, 9, 9, 11, 0, 0, TimeSpan.Zero), null, null,
            [new(lots.Source.Id, consumedQuantity)], [new(output.Id, consumedQuantity)]);

        using var response = await client.PostAsJsonAsync(EventPath(setup.Organization.Id), request, cancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.Conflict)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
            Assert.Equal("TRACEABILITY_EVENT_CONFLICT", problem["errorCode"]!.GetValue<string>());
            Assert.Contains(lots.Source.Id.ToString(),
                problem["detail"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        await AssertSourceQuantityUnchangedAsync(client, setup, lots.Source, cancellationToken);
    }

    private static async Task AssertSourceQuantityUnchangedAsync(
        HttpClient client, AuthorizedSetup setup, LotResponse source, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{setup.Organization.Id}/lots/{source.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var persisted = await response.Content.ReadFromJsonAsync<LotResponse>(cancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(source.Id, persisted.Id);
        Assert.Equal(100m, persisted.Quantity);
        Assert.Equal(source.Quantity, persisted.Quantity);
        Assert.Equal("KG", persisted.UnitCode);
    }

    private ApiWebApplicationFactory CreateFactory() => new(new Dictionary<string, string?>
    {
        ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
        ["RateLimiting:Authentication:PermitLimit"] = "100",
    });

    private static async Task<SplitLots> CreateSplitLotsAsync(
        HttpClient client, AuthorizedSetup setup, CancellationToken cancellationToken)
    {
        var source = await CreateLotAsync(client, setup, "OL-SRC", 100m, cancellationToken);
        var x = await CreateLotAsync(client, setup, "OL-X", 20m, cancellationToken);
        var y = await CreateLotAsync(client, setup, "OL-Y", 30m, cancellationToken);
        var z = await CreateLotAsync(client, setup, "OL-Z", 40m, cancellationToken);
        return new SplitLots(source, x, y, z);
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

    private static CreateTraceabilityEventRequest SplitRequest(AuthorizedSetup setup, SplitLots lots) =>
        new("SPLIT", setup.Organization.LocationId,
            new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero), null, null,
            [new(lots.Source.Id, 90m)],
            [new(lots.X.Id, 20m), new(lots.Y.Id, 30m), new(lots.Z.Id, 40m)]);

    private static async Task<TraceabilityEventResponse> CreateSplitAsync(
        HttpClient client, AuthorizedSetup setup, SplitLots lots, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            EventPath(setup.Organization.Id), SplitRequest(setup, lots), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var split = await response.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken);
        Assert.NotNull(split);
        return split;
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

    private sealed record SplitLots(LotResponse Source, LotResponse X, LotResponse Y, LotResponse Z)
    {
        public LotResponse[] Outputs => [X, Y, Z];
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
        var email = $"split-api-{userId:N}@example.com";
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
