// TRC-016: A and B deliberately use IDENTICAL lot numbers: OL-001 -> OIL-001 -> BOT-001.
// Structurally identical PRESS/BOTTLE chains with different IDs and quantities must stay
// isolated by the route organization, including for a user who belongs to both tenants.
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
public sealed class CrossTenantTraceabilityEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Theory]
    [InlineData(false, "backward")]
    [InlineData(false, "forward")]
    [InlineData(true, "backward")]
    [InlineData(true, "forward")]
    public async Task IdenticalLotNumbersReturnOnlyRouteOrganizationNodesEdgesAndQuantities(
        bool useOrganizationB, string direction)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        var (a, b) = await CreateTwinChainsAsync(client, cancellationToken);
        var expected = useOrganizationB ? b : a;
        var foreign = useOrganizationB ? a : b;
        await AuthenticateAsync(client, expected.Setup.Account, cancellationToken);
        var root = direction == "backward" ? expected.Bottles : expected.Olives;

        var graph = await ReadGraphAsync(client, expected.Setup.Organization.Id, root.Id,
            direction, cancellationToken);

        Assert.Equal(root.Id, graph.RootLotId);
        AssertIsolatedChain(graph, expected, foreign);
    }

    [Fact]
    public async Task DualMemberBackwardUsesEachRouteOrganizationWithTheSameLogin()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        var (a, b) = await CreateTwinChainsAsync(client, cancellationToken);
        var dualMember = await CreateAccountAsync();
        await AddMembershipAndRoleAsync(dualMember.UserId, a.Setup.Organization.Id, StandardRoleIds.Producer);
        await AddMembershipAndRoleAsync(dualMember.UserId, b.Setup.Organization.Id, StandardRoleIds.Producer);
        await AuthenticateAsync(client, dualMember, cancellationToken);

        // Both requests use the same token: access to B must not widen the graph from A.
        var graphA = await ReadGraphAsync(client, a.Setup.Organization.Id, a.Bottles.Id,
            "backward", cancellationToken);
        AssertIsolatedChain(graphA, a, b);
        Assert.Equal(a.Bottles.Id, graphA.RootLotId);

        var graphB = await ReadGraphAsync(client, b.Setup.Organization.Id, b.Bottles.Id,
            "backward", cancellationToken);
        AssertIsolatedChain(graphB, b, a);
        Assert.Equal(b.Bottles.Id, graphB.RootLotId);
    }

    [Fact]
    public async Task EventInOrganizationARejectsInputFromBDespiteIdenticalLotNumber()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        var (a, b) = await CreateTwinChainsAsync(client, cancellationToken);
        await AuthenticateAsync(client, a.Setup.Account, cancellationToken);
        var output = await CreateLotAsync(client, a.Setup, "BOT-EXTRA", 1m, "PCS", cancellationToken);
        var request = new CreateTraceabilityEventRequest("BOTTLE", a.Setup.Organization.LocationId,
            new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero), null, null,
            [new(b.Oil.Id, 1m)], [new(output.Id, 1m)]);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var eventCount = await context.TraceabilityEvents
            .CountAsync(e => e.OrganizationId == a.Setup.Organization.Id, cancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventPath(a.Setup.Organization.Id), request, cancellationToken);

        // The caller may create events in A, but B's lot is an invalid reference in A.
        // The writer's tenant-scoped lot lookup raises validation, mapped by the API to 400.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
        Assert.Equal("TRACEABILITY_EVENT_VALIDATION_FAILED", problem["errorCode"]!.GetValue<string>());
        Assert.Equal("One or more referenced lots do not exist in this organization.",
            problem["detail"]!.GetValue<string>());
        Assert.Equal(eventCount, await context.TraceabilityEvents
            .CountAsync(e => e.OrganizationId == a.Setup.Organization.Id, cancellationToken));

        // A's same-number oil has unused quantity. Replacing only the foreign input ID
        // proves the rejection is caused by tenant ownership, not by another invalid field.
        using var ownInputResponse = await client.PostAsJsonAsync(EventPath(a.Setup.Organization.Id),
            request with { Inputs = [new(a.Oil.Id, 1m)] }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, ownInputResponse.StatusCode);
    }

    private static void AssertIsolatedChain(TraceGraphResponse graph, Chain expected, Chain foreign)
    {
        Assert.Equal(3, graph.Nodes.Count);
        foreach (var lot in expected.Lots)
        {
            Assert.Equal(new TraceNodeResponse(lot.Id, lot.LotNumber, lot.ArticleId, lot.Quantity, lot.UnitCode),
                Assert.Single(graph.Nodes, node => node.LotId == lot.Id));
        }
        Assert.Equal(2, graph.Edges.Count);
        Assert.Contains(new TraceEdgeResponse(expected.Press.Id, "PRESS", expected.Press.OccurredAt,
            expected.Olives.Id, expected.Oil.Id), graph.Edges);
        Assert.Contains(new TraceEdgeResponse(expected.Bottle.Id, "BOTTLE", expected.Bottle.OccurredAt,
            expected.Oil.Id, expected.Bottles.Id), graph.Edges);
        foreach (var lot in foreign.Lots)
        {
            Assert.DoesNotContain(graph.Nodes, node => node.LotId == lot.Id);
            Assert.DoesNotContain(graph.Edges, edge => edge.FromLotId == lot.Id || edge.ToLotId == lot.Id);
        }
        Assert.DoesNotContain(graph.Edges, edge => edge.EventId == foreign.Press.Id || edge.EventId == foreign.Bottle.Id);
    }

    private async Task<(Chain A, Chain B)> CreateTwinChainsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var a = await CreateChainAsync(client, 100m, 25m, 20m, 80m, cancellationToken);
        var b = await CreateChainAsync(client, 240m, 65m, 60m, 180m, cancellationToken);
        Assert.NotEqual(a.Setup.Organization.Id, b.Setup.Organization.Id);
        for (var i = 0; i < a.Lots.Length; i++)
        {
            Assert.Equal(a.Lots[i].LotNumber, b.Lots[i].LotNumber);
            Assert.NotEqual(a.Lots[i].Id, b.Lots[i].Id);
            Assert.NotEqual(a.Lots[i].Quantity, b.Lots[i].Quantity);
        }
        Assert.NotEqual(a.Press.Id, b.Press.Id);
        Assert.NotEqual(a.Bottle.Id, b.Bottle.Id);
        return (a, b);
    }

    private async Task<Chain> CreateChainAsync(HttpClient client, decimal oliveQuantity, decimal oilQuantity,
        decimal bottledOilQuantity, decimal bottleQuantity, CancellationToken cancellationToken)
    {
        var setup = await CreateSetupAsync();
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        var olives = await CreateLotAsync(client, setup, "OL-001", oliveQuantity, "KG", cancellationToken);
        var oil = await CreateLotAsync(client, setup, "OIL-001", oilQuantity, "L", cancellationToken);
        var bottles = await CreateLotAsync(client, setup, "BOT-001", bottleQuantity, "PCS", cancellationToken);
        var press = await CreateEventAsync(client, setup, "PRESS", 10,
            new(olives.Id, oliveQuantity), new(oil.Id, oilQuantity), cancellationToken);
        var bottle = await CreateEventAsync(client, setup, "BOTTLE", 11,
            new(oil.Id, bottledOilQuantity), new(bottles.Id, bottleQuantity), cancellationToken);
        return new Chain(setup, olives, oil, bottles, press, bottle);
    }

    private ApiWebApplicationFactory CreateFactory() => new(new Dictionary<string, string?>
    {
        ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
        ["RateLimiting:Authentication:PermitLimit"] = "100",
    });

    private static async Task<LotResponse> CreateLotAsync(HttpClient client, Setup setup,
        string lotNumber, decimal quantity, string unitCode, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/organizations/{setup.Organization.Id}/lots",
            new CreateLotRequest(setup.Article.Id, lotNumber, quantity, unitCode), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lot = await response.Content.ReadFromJsonAsync<LotResponse>(cancellationToken);
        Assert.NotNull(lot);
        Assert.Equal(setup.Organization.Id, lot.OrganizationId);
        Assert.Equal(lotNumber, lot.LotNumber);
        Assert.Equal(quantity, lot.Quantity);
        return lot;
    }

    private static async Task<TraceabilityEventResponse> CreateEventAsync(HttpClient client, Setup setup,
        string eventTypeCode, int hour, TraceabilityEventLotRequest input, TraceabilityEventLotRequest output,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(EventPath(setup.Organization.Id),
            new CreateTraceabilityEventRequest(eventTypeCode, setup.Organization.LocationId,
                new DateTimeOffset(2026, 9, 9, hour, 0, 0, TimeSpan.Zero), null, null, [input], [output]),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TraceabilityEventResponse>(cancellationToken);
        Assert.NotNull(result);
        Assert.Equal(setup.Organization.Id, result.OrganizationId);
        Assert.Equal(eventTypeCode, result.EventTypeCode);
        return result;
    }

    private static async Task<TraceGraphResponse> ReadGraphAsync(HttpClient client, Guid organizationId,
        Guid lotId, string direction, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/v1/organizations/{organizationId}/lots/{lotId}/traceability/{direction}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var graph = await response.Content.ReadFromJsonAsync<TraceGraphResponse>(cancellationToken);
        Assert.NotNull(graph);
        return graph;
    }

    private static string EventPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/traceability/events";

    private async Task<Setup> CreateSetupAsync()
    {
        var account = await CreateAccountAsync();
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        await using var organizations = database.CreateLotApiOrganizationsDbContext();
        organizations.Organizations.Add(Organization.Create(organizationId,
            $"Cross Tenant {organizationId:N}", null, null, null, null, now));
        organizations.Locations.Add(Location.Create(locationId, organizationId,
            "Cross Tenant Location", null, null, null, null, null, now));
        await organizations.SaveChangesAsync();

        var productId = Guid.NewGuid();
        var product = Product.Create(productId, $"CROSS-TENANT-{productId:N}", "Cross Tenant Product", now);
        var articleId = Guid.NewGuid();
        var article = Article.Create(articleId, organizationId, productId, $"SKU-{articleId:N}", null, now);
        await using var catalog = database.CreateLotApiCatalogDbContext();
        catalog.Products.Add(product);
        catalog.Articles.Add(article);
        await catalog.SaveChangesAsync();
        await AddMembershipAndRoleAsync(account.UserId, organizationId, StandardRoleIds.Producer);
        return new Setup(account, new TestOrganization(organizationId, locationId), article);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"cross-tenant-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Cross Tenant", "Test", now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword), now);
        await using var identity = database.CreateLotApiIdentityDbContext();
        identity.Users.Add(user);
        identity.UserCredentials.Add(credential);
        await identity.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private async Task AddMembershipAndRoleAsync(Guid userId, Guid organizationId, Guid roleId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var identity = database.CreateLotApiIdentityDbContext();
        identity.OrganizationMemberships.Add(OrganizationMembership.Create(userId, organizationId, now));
        identity.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(), userId, organizationId, roleId, locationId: null, now));
        await identity.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(HttpClient client, TestAccount account, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { account.Email, Password = ValidPassword }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<FoodTraceability.Api.Contracts.Authentication.AuthenticationTokenResponse>(cancellationToken);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private sealed record TestAccount(Guid UserId, string Email);
    private sealed record TestOrganization(Guid Id, Guid LocationId);
    private sealed record Setup(TestAccount Account, TestOrganization Organization, Article Article);
    private sealed record Chain(Setup Setup, LotResponse Olives, LotResponse Oil, LotResponse Bottles,
        TraceabilityEventResponse Press, TraceabilityEventResponse Bottle)
    {
        public LotResponse[] Lots => [Olives, Oil, Bottles];
    }
}
