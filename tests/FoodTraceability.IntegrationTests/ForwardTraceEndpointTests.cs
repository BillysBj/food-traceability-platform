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

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class ForwardTraceEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task ForwardOlivesContainOilAndBottlesWithRootAndDirectedEventEdges()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        // Development plan's mandatory chain, recorded through the real lot/event APIs.
        var olives = await CreateLotAsync(client, setup, "OL-001", 100m, "KG", factory.RequestCancellationToken);
        var oil = await CreateLotAsync(client, setup, "OIL-001", 20m, "L", factory.RequestCancellationToken);
        var bottles = await CreateLotAsync(client, setup, "BOT-001", 100m, "PCS", factory.RequestCancellationToken);
        var press = await CreateEventAsync(client, setup, "PRESS",
            [new(olives.Id, 100m)], [new(oil.Id, 20m)], factory.RequestCancellationToken);
        var bottle = await CreateEventAsync(client, setup, "BOTTLE",
            [new(oil.Id, 20m)], [new(bottles.Id, 100m)], factory.RequestCancellationToken);

        var graph = await ReadGraphAsync(client, setup.Organization.Id, olives.Id, factory.RequestCancellationToken);

        // Assert the immediate descendant first: the one-level mutation must still find oil.
        Assert.Contains(graph.Nodes, node => node.LotId == oil.Id && node.LotNumber == "OIL-001");
        Assert.Contains(graph.Nodes, node => node.LotId == bottles.Id && node.LotNumber == "BOT-001");
        Assert.Equal(olives.Id, graph.RootLotId);
        Assert.Equal(3, graph.Nodes.Count);
        AssertNode(graph, olives);
        AssertNode(graph, oil);
        AssertNode(graph, bottles);
        Assert.Equal(2, graph.Edges.Count);
        Assert.Contains(new TraceEdgeResponse(press.Id, "PRESS", press.OccurredAt, olives.Id, oil.Id), graph.Edges);
        Assert.Contains(new TraceEdgeResponse(bottle.Id, "BOTTLE", bottle.OccurredAt, oil.Id, bottles.Id), graph.Edges);
        AssertUniqueGraph(graph);
    }

    [Fact]
    public async Task BackwardAndForwardOnTheSameChainReturnIdenticalDirectedEdges()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var olives = await CreateLotAsync(client, setup, "OL-001", 100m, "KG", factory.RequestCancellationToken);
        var oil = await CreateLotAsync(client, setup, "OIL-001", 20m, "L", factory.RequestCancellationToken);
        var bottles = await CreateLotAsync(client, setup, "BOT-001", 100m, "PCS", factory.RequestCancellationToken);
        var press = await CreateEventAsync(client, setup, "PRESS",
            [new(olives.Id, 100m)], [new(oil.Id, 20m)], factory.RequestCancellationToken);
        var bottle = await CreateEventAsync(client, setup, "BOTTLE",
            [new(oil.Id, 20m)], [new(bottles.Id, 100m)], factory.RequestCancellationToken);

        var forward = await ReadGraphAsync(client, setup.Organization.Id, olives.Id, factory.RequestCancellationToken);
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{setup.Organization.Id}/lots/{bottles.Id}/traceability/backward",
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var backward = await response.Content.ReadFromJsonAsync<TraceGraphResponse>(factory.RequestCancellationToken);
        Assert.NotNull(backward);

        // Compare the entire edges, including their input -> output direction and event metadata.
        Assert.Equal(backward.Edges.OrderBy(edge => edge.EventId), forward.Edges.OrderBy(edge => edge.EventId));
        Assert.Equal(2, forward.Edges.Count);
        Assert.Contains(new TraceEdgeResponse(press.Id, "PRESS", press.OccurredAt, olives.Id, oil.Id), forward.Edges);
        Assert.Contains(new TraceEdgeResponse(bottle.Id, "BOTTLE", bottle.OccurredAt, oil.Id, bottles.Id), forward.Edges);
        AssertUniqueGraph(backward);
        AssertUniqueGraph(forward);
    }

    [Fact]
    public async Task LotWithoutDescendantsReturnsOnlyItsRoot()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory(maxNodes: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var lot = await CreateLotAsync(client, setup, "ROOT", 10m, "KG", factory.RequestCancellationToken);

        var graph = await ReadGraphAsync(client, setup.Organization.Id, lot.Id, factory.RequestCancellationToken);

        Assert.Equal(lot.Id, graph.RootLotId);
        Assert.Single(graph.Nodes);
        AssertNode(graph, lot);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task DiamondReturnsSharedDescendantExactlyOnceAndEveryEdgeOnce()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var a = await CreateLotAsync(client, setup, "A", 10m, "KG", factory.RequestCancellationToken);
        var b = await CreateLotAsync(client, setup, "B", 5m, "KG", factory.RequestCancellationToken);
        var c = await CreateLotAsync(client, setup, "C", 5m, "KG", factory.RequestCancellationToken);
        var d = await CreateLotAsync(client, setup, "D", 10m, "KG", factory.RequestCancellationToken);
        var ab = await CreateEventAsync(client, setup, "PROCESS", [new(a.Id, 5m)], [new(b.Id, 5m)], factory.RequestCancellationToken);
        var ac = await CreateEventAsync(client, setup, "PROCESS", [new(a.Id, 5m)], [new(c.Id, 5m)], factory.RequestCancellationToken);
        var mix = await CreateEventAsync(client, setup, "MIX",
            [new(b.Id, 5m), new(c.Id, 5m)], [new(d.Id, 10m)], factory.RequestCancellationToken);

        var graph = await ReadGraphAsync(client, setup.Organization.Id, a.Id, factory.RequestCancellationToken);

        Assert.Single(graph.Nodes, node => node.LotId == d.Id);
        Assert.Equal(4, graph.Nodes.Count);
        Assert.Equal(4, graph.Edges.Count);
        Assert.Contains(new TraceEdgeResponse(ab.Id, "PROCESS", ab.OccurredAt, a.Id, b.Id), graph.Edges);
        Assert.Contains(new TraceEdgeResponse(ac.Id, "PROCESS", ac.OccurredAt, a.Id, c.Id), graph.Edges);
        Assert.Contains(new TraceEdgeResponse(mix.Id, "MIX", mix.OccurredAt, b.Id, d.Id), graph.Edges);
        Assert.Contains(new TraceEdgeResponse(mix.Id, "MIX", mix.OccurredAt, c.Id, d.Id), graph.Edges);
        AssertUniqueGraph(graph);
    }

    [Fact]
    public async Task ForeignAndMissingLotIdsReturnIndistinguishable404Responses()
    {
        var own = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var foreign = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var foreignClient = factory.CreateClient();
        await AuthenticateAsync(foreignClient, foreign.Account, factory.RequestCancellationToken);
        var foreignLot = await CreateLotAsync(foreignClient, foreign, "FOREIGN", 1m, "KG", factory.RequestCancellationToken);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, own.Account, factory.RequestCancellationToken);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "forward-not-found-comparison");

        using var foreignResponse = await client.GetAsync(
            TracePath(own.Organization.Id, foreignLot.Id), factory.RequestCancellationToken);
        using var missingResponse = await client.GetAsync(
            TracePath(own.Organization.Id, Guid.NewGuid()), factory.RequestCancellationToken);

        var foreignBody = await AssertProblemAsync(foreignResponse, HttpStatusCode.NotFound,
            "TRACEABILITY_TRACE_NOT_FOUND", factory.RequestCancellationToken);
        var missingBody = await AssertProblemAsync(missingResponse, HttpStatusCode.NotFound,
            "TRACEABILITY_TRACE_NOT_FOUND", factory.RequestCancellationToken);
        Assert.Equal(foreignResponse.Headers.ToString(), missingResponse.Headers.ToString());
        Assert.Equal(foreignResponse.Content.Headers.ToString(), missingResponse.Content.Headers.ToString());
        // Match the existing anti-enumeration tests: only per-request diagnostic ids differ.
        Assert.Equal(RemoveCorrelationIdentifiers(foreignBody), RemoveCorrelationIdentifiers(missingBody));
    }

    [Fact]
    public async Task UserWithoutTraceReadReturns403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Laboratory);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            TracePath(setup.Organization.Id, Guid.NewGuid()), factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationPermissionDoesNotGrantAccessToAnotherOrganization()
    {
        var own = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var foreign = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, own.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            TracePath(foreign.Organization.Id, Guid.NewGuid()), factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
    }

    [Theory]
    [InlineData(2, HttpStatusCode.Conflict)]
    [InlineData(3, HttpStatusCode.OK)]
    public async Task ConfiguredNodeLimitIncludesRootAndRejectsOnlyOversizedGraphs(int maxNodes, HttpStatusCode expected)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory(maxNodes);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var a = await CreateLotAsync(client, setup, "A", 10m, "KG", factory.RequestCancellationToken);
        var b = await CreateLotAsync(client, setup, "B", 10m, "KG", factory.RequestCancellationToken);
        var c = await CreateLotAsync(client, setup, "C", 10m, "KG", factory.RequestCancellationToken);
        await CreateEventAsync(client, setup, "PROCESS", [new(a.Id, 10m)], [new(b.Id, 10m)], factory.RequestCancellationToken);
        await CreateEventAsync(client, setup, "PROCESS", [new(b.Id, 10m)], [new(c.Id, 10m)], factory.RequestCancellationToken);

        using var response = await client.GetAsync(TracePath(setup.Organization.Id, a.Id), factory.RequestCancellationToken);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Conflict)
        {
            var body = await AssertProblemAsync(response, expected, "TRACEABILITY_TRACE_TOO_LARGE", factory.RequestCancellationToken);
            Assert.False(body.ContainsKey("nodes"));
            Assert.False(body.ContainsKey("edges"));
            Assert.Contains("2 nodes", body["detail"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        else
        {
            var graph = await response.Content.ReadFromJsonAsync<TraceGraphResponse>(factory.RequestCancellationToken);
            Assert.NotNull(graph);
            Assert.Equal(3, graph.Nodes.Count);
            Assert.Equal(2, graph.Edges.Count);
        }
    }

    private ApiWebApplicationFactory CreateFactory(int? maxNodes = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
            ["RateLimiting:Authentication:PermitLimit"] = "100",
        };
        if (maxNodes is int limit)
        {
            configuration["Traceability:Graph:MaxNodes"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new ApiWebApplicationFactory(configuration);
    }

    private static async Task<LotResponse> CreateLotAsync(
        HttpClient client, AuthorizedSetup setup, string lotNumber, decimal quantity,
        string unitCode, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{setup.Organization.Id}/lots",
            new CreateLotRequest(setup.Article.Id, lotNumber, quantity, unitCode), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LotResponse>(cancellationToken))!;
    }

    private static async Task<TraceabilityEventResponse> CreateEventAsync(
        HttpClient client, AuthorizedSetup setup, string eventTypeCode,
        IReadOnlyList<TraceabilityEventLotRequest> inputs, IReadOnlyList<TraceabilityEventLotRequest> outputs,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{setup.Organization.Id}/traceability/events",
            new CreateTraceabilityEventRequest(eventTypeCode, setup.Organization.LocationId,
                new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero), null, null, inputs, outputs),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken))!;
    }

    private static async Task<TraceGraphResponse> ReadGraphAsync(
        HttpClient client, Guid organizationId, Guid lotId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(TracePath(organizationId, lotId), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TraceGraphResponse>(cancellationToken))!;
    }

    private static void AssertNode(TraceGraphResponse graph, LotResponse lot) =>
        Assert.Equal(new TraceNodeResponse(lot.Id, lot.LotNumber, lot.ArticleId, lot.Quantity, lot.UnitCode),
            Assert.Single(graph.Nodes, node => node.LotId == lot.Id));

    private static void AssertUniqueGraph(TraceGraphResponse graph)
    {
        Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(node => node.LotId).Distinct().Count());
        Assert.Equal(graph.Edges.Count, graph.Edges.Select(edge => (edge.EventId, edge.FromLotId, edge.ToLotId)).Distinct().Count());
    }

    private static async Task<JsonObject> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode expectedStatus, string errorCode, CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
        Assert.Equal(errorCode, body["errorCode"]!.GetValue<string>());
        return body;
    }

    private static string RemoveCorrelationIdentifiers(JsonObject body)
    {
        Assert.True(body.Remove("correlationId"));
        Assert.True(body.Remove("traceId"));
        return body.ToJsonString();
    }

    private static string TracePath(Guid organizationId, Guid lotId) =>
        $"/api/v1/organizations/{organizationId}/lots/{lotId}/traceability/forward";
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
        var email = $"forward-api-{userId:N}@example.com";
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
