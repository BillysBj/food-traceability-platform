using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.Authentication;
using FoodTraceability.Api.Contracts.Traces;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit.Abstractions;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class TraceabilityPerformanceBaselineTests(
    PostgreSqlContainerFixture database,
    ITestOutputHelper output)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task RequiredTraceabilityColumnsHaveLeadingIndexes()
    {
        // AGENTS.md section 37, scoped by TRC-017. lot_number alone is deliberately
        // excluded: its existing functional unique index leads with organization_id.
        string[] requiredColumns =
        [
            "trace.event_input(lot_id)",
            "trace.event_output(lot_id)",
            "trace.traceability_event(occurred_at)",
            "trace.traceability_event(organization_id)",
            "trace.lot(organization_id)",
        ];

        await using var connection = new NpgsqlConnection(database.LotApiConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT DISTINCT n.nspname || '.' || t.relname || '(' || a.attname || ')'
              FROM pg_catalog.pg_index i
              JOIN pg_catalog.pg_class t ON t.oid = i.indrelid
              JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
              JOIN pg_catalog.pg_attribute a
                ON a.attrelid = t.oid AND a.attnum = i.indkey[0]
             WHERE n.nspname = 'trace'
               AND i.indisvalid AND i.indisready AND i.indnkeyatts > 0
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var indexedColumns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            indexedColumns.Add(reader.GetString(0));
        }

        // indkey is zero-based: only its first key counts, never a later key or
        // INCLUDE column. A functional trailing key does not invalidate the first key.
        var missing = requiredColumns.Except(indexedColumns, StringComparer.Ordinal).ToArray();
        output.WriteLine("Required leading index columns: {0}", string.Join(", ", requiredColumns));
        output.WriteLine("Missing leading index columns: {0}", string.Join(", ", missing));
        Assert.True(missing.Length == 0,
            $"Missing leading index columns: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("backward")]
    [InlineData("forward")]
    public async Task TraceCommandCountIsIndependentOfGraphSize(string direction)
    {
        var setup = await SeedSetupAsync();
        var small = await SeedChainAsync(setup, "SMALL", 3);
        var large = await SeedChainAsync(setup, "LARGE", 300);
        var counter = new CommandCounter();
        await using var factory = new ApiWebApplicationFactory(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
            },
            configureTestServices: services =>
            {
                services.AddSingleton(counter);
                AddCounter<PlatformDbContext>(services);
                AddCounter<IdentityDbContext>(services);
                AddCounter<OrganizationsDbContext>(services);
                AddCounter<CatalogDbContext>(services);
                AddCounter<TraceabilityDbContext>(services);
            });
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { setup.Email, Password = ValidPassword }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthenticationTokenResponse>(cancellationToken);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Only the HTTP trace requests are measured, excluding seeding and login.
        // No elapsed-time assertion: this is a deterministic guard against N+1 queries.
        var smallCount = await MeasureTraceAsync(small);
        var largeCount = await MeasureTraceAsync(large);
        output.WriteLine("{0}: 3 lots = {1} commands; 300 lots = {2} commands.",
            direction, smallCount, largeCount);
        Assert.Equal(smallCount, largeCount);

        async Task<int> MeasureTraceAsync(Guid[] lotIds)
        {
            var root = direction == "backward" ? lotIds[^1] : lotIds[0];
            counter.Start();
            TraceGraphResponse? graph;
            IReadOnlyDictionary<string, int> commands;
            try
            {
                using var response = await client.GetAsync(
                    $"/api/v1/organizations/{setup.OrganizationId}/lots/{root}/traceability/{direction}",
                    cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                graph = await response.Content.ReadFromJsonAsync<TraceGraphResponse>(cancellationToken);
            }
            finally
            {
                commands = counter.Stop();
            }

            Assert.NotNull(graph);
            Assert.Equal(root, graph.RootLotId);
            Assert.Equal(lotIds.Length, graph.Nodes.Count);
            Assert.Equal(lotIds.Order(), graph.Nodes.Select(node => node.LotId).Order());
            Assert.Equal(lotIds.Length - 1, graph.Edges.Count);
            Assert.Equal(
                lotIds.Zip(lotIds.Skip(1)).Order(),
                graph.Edges.Select(edge => (edge.FromLotId, edge.ToLotId)).Order());
            // Avoid a vacuous 0 == 0 if interceptor registration stops working.
            Assert.True(commands.GetValueOrDefault(nameof(TraceabilityDbContext)) > 0,
                "The interceptor must observe the real trace queries.");
            Assert.True(commands.GetValueOrDefault(nameof(IdentityDbContext)) > 0,
                "The interceptor must also observe HTTP authentication/authorization queries.");
            output.WriteLine("{0}, {1} lots: {2}", direction, lotIds.Length,
                string.Join(", ", commands.OrderBy(pair => pair.Key)
                    .Select(pair => $"{pair.Key}={pair.Value}")));
            return commands.Values.Sum();
        }
    }

    private static void AddCounter<TContext>(IServiceCollection services) where TContext : DbContext =>
        services.ConfigureDbContext<TContext>((provider, options) =>
            options.AddInterceptors(provider.GetRequiredService<CommandCounter>()));

    private async Task<Setup> SeedSetupAsync()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var organizationId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"performance-{userId:N}@example.com";
        await using (var organizations = database.CreateLotApiOrganizationsDbContext())
        {
            organizations.Organizations.Add(Organization.Create(organizationId,
                $"Performance {organizationId:N}", null, null, null, null, now));
            organizations.Locations.Add(Location.Create(locationId, organizationId,
                "Performance location", null, null, null, null, null, now));
            await organizations.SaveChangesAsync();
        }

        await using (var identity = database.CreateLotApiIdentityDbContext())
        {
            identity.Users.Add(User.Create(userId, EmailAddress.Create(email), "Performance", "Test", now));
            var credential = UserCredential.Create(userId, "temporary-hash", now, now);
            credential.ChangePasswordHash(
                new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword), now);
            identity.UserCredentials.Add(credential);
            identity.OrganizationMemberships.Add(OrganizationMembership.Create(userId, organizationId, now));
            identity.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(), userId, organizationId, StandardRoleIds.Producer, null, now));
            await identity.SaveChangesAsync();
        }

        await using var catalog = database.CreateLotApiCatalogDbContext();
        var productId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        catalog.Products.Add(Product.Create(productId, $"PERF-{productId:N}", "Performance product", now));
        catalog.Articles.Add(Article.Create(articleId, organizationId, productId,
            $"PERF-{articleId:N}", null, now));
        var unitId = await catalog.Units.Where(unit => unit.Code == UnitCode.Create("KG"))
            .Select(unit => unit.Id).SingleAsync();
        await catalog.SaveChangesAsync();
        return new Setup(organizationId, locationId, userId, email, articleId, unitId);
    }

    private async Task<Guid[]> SeedChainAsync(Setup setup, string prefix, int length)
    {
        await using var trace = database.CreateLotApiTraceabilityDbContext();
        var eventTypeId = await trace.EventTypes.Where(type => type.Code == EventTypeCode.Create("PROCESS"))
            .Select(type => type.Id).SingleAsync();
        var start = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var lots = Enumerable.Range(0, length).Select(index => Lot.Create(
            Guid.NewGuid(), setup.OrganizationId, setup.ArticleId, $"{prefix}-{index:D3}",
            1m, setup.UnitId, start.AddMinutes(index))).ToArray();
        trace.Lots.AddRange(lots);
        await trace.SaveChangesAsync();

        for (var index = 1; index < lots.Length; index++)
        {
            var occurredAt = start.AddMinutes(index);
            var traceEvent = TraceabilityEvent.Create(Guid.NewGuid(), eventTypeId,
                setup.OrganizationId, setup.LocationId, occurredAt, null, null, setup.UserId, occurredAt,
                [EventInput.Create(Guid.NewGuid(), lots[index - 1].Id, 1m, setup.UnitId)],
                [EventOutput.Create(Guid.NewGuid(), lots[index].Id, 1m, setup.UnitId)]);
            trace.TraceabilityEvents.Add(traceEvent);
            // The owning writer normally sets these tenant shadow properties.
            foreach (var input in traceEvent.Inputs)
                trace.Entry(input).Property("OrganizationId").CurrentValue = setup.OrganizationId;
            foreach (var eventOutput in traceEvent.Outputs)
                trace.Entry(eventOutput).Property("OrganizationId").CurrentValue = setup.OrganizationId;
        }

        await trace.SaveChangesAsync();
        return lots.Select(lot => lot.Id).ToArray();
    }

    private sealed record Setup(
        Guid OrganizationId, Guid LocationId, Guid UserId, string Email, Guid ArticleId, Guid UnitId);

    // One counter per factory; this database collection executes serially. Count all
    // EF commands (reader/scalar/non-query, sync/async), including raw SQL through EF.
    // Transaction begin/commit and connection protocol messages are not DbCommands.
    private sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly ConcurrentDictionary<string, int> _commands = new();
        private int _active;

        public void Start()
        {
            _commands.Clear();
            Volatile.Write(ref _active, 1);
        }

        public IReadOnlyDictionary<string, int> Stop()
        {
            Volatile.Write(ref _active, 0);
            return new Dictionary<string, int>(_commands);
        }

        private void Count(CommandEventData eventData)
        {
            if (Volatile.Read(ref _active) == 1)
                _commands.AddOrUpdate(eventData.Context?.GetType().Name ?? "Unknown", 1, (_, count) => count + 1);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Count(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count(eventData);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Count(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Count(eventData);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Count(eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count(eventData);
            return ValueTask.FromResult(result);
        }
    }
}
