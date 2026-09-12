using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FoodTraceability.Api.Contracts.TraceabilityEvents;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class TraceabilityEventEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";
    private const string PressCode = "PRESS";

    private static readonly Guid PressId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");
    private static readonly Guid KilogramId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private static readonly TimeSpan DatabaseStateTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CreateWithSubMicrosecondOccurredAtReturnsExactlyThePersistedUtcTicks()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 8m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var microsecondValue = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero)
            .AddTicks(1_234_560);
        var request = ValidRequest(
            setup.Organization.LocationId,
            [new(input.Id, 10m)],
            [new(output.Id, 8m)]) with
        {
            OccurredAt = microsecondValue.AddTicks(7)
        };

        using var createResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id), request, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadEventAsync(createResponse, factory.RequestCancellationToken);

        using var getResponse = await client.GetAsync(
            createResponse.Headers.Location, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var read = await ReadEventAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(created.OccurredAt.UtcTicks, read.OccurredAt.UtcTicks);
        Assert.Equal(microsecondValue.UtcTicks, created.OccurredAt.UtcTicks);
    }

    [Fact]
    public async Task CreateWithInputAndOutputReturns201AndLocationCanBeRead()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 8m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 10m)],
                [new(output.Id, 8m)]),
            factory.RequestCancellationToken);
        var created = await ReadEventAsync(
            createResponse,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(
            EventPath(setup.Organization.Id, created.Id),
            createResponse.Headers.Location?.OriginalString);
        Assert.Equal(setup.Account.UserId, created.CreatedBy);

        using var getResponse = await client.GetAsync(
            createResponse.Headers.Location,
            factory.RequestCancellationToken);
        var read = await ReadEventAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, read.Id);
        Assert.Equal(created.OrganizationId, read.OrganizationId);
        Assert.Equal(created.EventTypeCode, read.EventTypeCode);
        Assert.Equal(created.LocationId, read.LocationId);
        Assert.Equal(created.OccurredAt, read.OccurredAt);
        Assert.Equal(created.ExternalReference, read.ExternalReference);
        Assert.Equal(created.Description, read.Description);
        Assert.Equal(created.CreatedBy, read.CreatedBy);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
        Assert.Equal(created.Inputs.ToArray(), read.Inputs.ToArray());
        Assert.Equal(created.Outputs.ToArray(), read.Outputs.ToArray());
    }

    [Fact]
    public async Task CreateWithMultipleInputsAndMultipleOutputsPersistsEveryLine()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Processor);
        var inputOne = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 7m);
        var inputTwo = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 9m);
        var outputOne = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 6m);
        var outputTwo = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(inputOne.Id, 7m), new(inputTwo.Id, 9m)],
                [new(outputOne.Id, 6m), new(outputTwo.Id, 10m)]),
            factory.RequestCancellationToken);
        var body = await ReadEventAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, body.Inputs.Count);
        Assert.Equal(2, body.Outputs.Count);
        Assert.All(body.Inputs, line => Assert.Equal(KilogramId, line.UnitId));
        Assert.All(body.Outputs, line => Assert.Equal(KilogramId, line.UnitId));
        Assert.Equal(
            new[] { inputOne.Id, inputTwo.Id }.Order(),
            body.Inputs.Select(line => line.LotId).Order());
        Assert.Equal(
            new[] { outputOne.Id, outputTwo.Id }.Order(),
            body.Outputs.Select(line => line.LotId).Order());

        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persisted = await context.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(traceabilityEvent => traceabilityEvent.Inputs)
            .Include(traceabilityEvent => traceabilityEvent.Outputs)
            .SingleAsync(traceabilityEvent => traceabilityEvent.Id == body.Id);
        Assert.Equal((2, 2), (persisted.Inputs.Count, persisted.Outputs.Count));
        Assert.All(persisted.Inputs, line => Assert.Equal(KilogramId, line.UnitId));
        Assert.All(persisted.Outputs, line => Assert.Equal(KilogramId, line.UnitId));
    }

    [Fact]
    public async Task EventWithOnlyInputsIsCreated()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 5m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 2m)],
                []),
            factory.RequestCancellationToken);
        var body = await ReadEventAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(body.Inputs);
        Assert.Empty(body.Outputs);
    }

    [Fact]
    public async Task EventWithOnlyOutputsIsCreated()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Bottler);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 5m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [],
                [new(output.Id, 5m)]),
            factory.RequestCancellationToken);
        var body = await ReadEventAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(body.Inputs);
        Assert.Single(body.Outputs);
    }

    [Fact]
    public async Task EventWithoutInputsOrOutputsReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(setup.Organization.LocationId, [], []),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownEventTypeCodeReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = ValidRequest(
            setup.Organization.LocationId,
            [],
            [new(output.Id, 1m)]) with
        {
            EventTypeCode = "NOT_A_REGISTERED_EVENT"
        };

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            request,
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Theory]
    [InlineData("BLOCK")]
    [InlineData("TRANSFER")]
    [InlineData("STORE")]
    [InlineData("RECEIVE")]
    public async Task DisallowedEventTypeCodeReturns400(string eventTypeCode)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = ValidRequest(
            setup.Organization.LocationId,
            [],
            [new(output.Id, 1m)]) with
        {
            EventTypeCode = eventTypeCode
        };

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            request,
            factory.RequestCancellationToken);

        using var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
        var detail = problem.RootElement.GetProperty("detail").GetString();
        Assert.Contains(eventTypeCode, detail, StringComparison.Ordinal);
        Assert.Contains("not allowed for traceability events", detail, StringComparison.Ordinal);

        await using var context = database.CreateLotApiTraceabilityDbContext();
        Assert.False(await context.TraceabilityEvents.AnyAsync(
            traceabilityEvent => traceabilityEvent.OrganizationId == setup.Organization.Id));
    }

    [Theory]
    [InlineData("NOT_A_REGISTERED_EVENT", "The referenced event type does not exist.")]
    [InlineData("QUALITY_RELEASE", "Event type 'QUALITY_RELEASE' is not allowed for traceability events.")]
    public async Task InvalidEventTypeCodeReturns400WithExactValidationMessage(
        string eventTypeCode,
        string expectedMessage)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = ValidRequest(
            setup.Organization.LocationId,
            [],
            [new(output.Id, 1m)]) with
        {
            EventTypeCode = eventTypeCode
        };

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            request,
            factory.RequestCancellationToken);

        using var problem = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
        Assert.Equal(expectedMessage, problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task LocationFromAnotherOrganizationReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var foreignOrganization = await CreateOrganizationAsync();
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                foreignOrganization.LocationId,
                [],
                [new(output.Id, 1m)]),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task InputLotFromAnotherOrganizationReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignArticle = await CreateArticleAsync(
            foreignOrganization.Id,
            setup.Product.Id);
        var foreignLot = await CreateLotAsync(
            foreignOrganization.Id,
            foreignArticle.Id,
            10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(foreignLot.Id, 1m)],
                []),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MissingInputLotReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(Guid.NewGuid(), 1m)],
                []),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "TRACEABILITY_EVENT_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task InputThatExceedsRemainingQuantityReturns409()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 8m)],
                []),
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 3m)],
                []),
            factory.RequestCancellationToken);
        using var problem = await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "TRACEABILITY_EVENT_CONFLICT",
            factory.RequestCancellationToken);

        Assert.Contains(
            input.Id.ToString(),
            problem.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InputThatExactlyConsumesRemainingQuantityIsCreated()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 8m)],
                []),
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 2m)],
                []),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SameLotCanBeConsumedByDifferentEventsWhileTotalFits()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 4m)],
                []),
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [new(input.Id, 5m)],
                []),
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var booked = await context.Set<EventInput>()
            .Where(line => line.LotId == input.Id)
            .SumAsync(line => line.Quantity);
        Assert.Equal(9m, booked);
    }

    [Fact]
    public async Task ProducerCannotCreateEventInAnotherOrganization()
    {
        var account = await CreateAccountAsync();
        var ownOrganization = await CreateOrganizationAsync();
        var targetOrganization = await CreateOrganizationAsync();
        var product = await CreateProductAsync();
        var targetArticle = await CreateArticleAsync(targetOrganization.Id, product.Id);
        var output = await CreateLotAsync(targetOrganization.Id, targetArticle.Id, 1m);
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            ownOrganization.Id,
            StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(targetOrganization.Id),
            ValidRequest(
                targetOrganization.LocationId,
                [],
                [new(output.Id, 1m)]),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutTraceabilityEventCreatePermissionGets403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.QualityManager);
        var output = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(
                setup.Organization.LocationId,
                [],
                [new(output.Id, 1m)]),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutTraceReadPermissionGets403()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Laboratory);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 1m);
        var traceabilityEvent = await CreatePersistedEventAsync(
            setup.Account.UserId,
            setup.Organization,
            input);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            EventPath(setup.Organization.Id, traceabilityEvent.Id),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task ForeignAndMissingEventIdsReturnIndistinguishable404Responses()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var foreignOrganization = await CreateOrganizationAsync();
        var foreignArticle = await CreateArticleAsync(foreignOrganization.Id, setup.Product.Id);
        var foreignLot = await CreateLotAsync(foreignOrganization.Id, foreignArticle.Id, 1m);
        var foreignEvent = await CreatePersistedEventAsync(
            setup.Account.UserId,
            foreignOrganization,
            foreignLot);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        client.DefaultRequestHeaders.Add(
            "X-Correlation-Id",
            "event-not-found-comparison");

        using var foreignResponse = await client.GetAsync(
            EventPath(setup.Organization.Id, foreignEvent.Id),
            factory.RequestCancellationToken);
        using var missingResponse = await client.GetAsync(
            EventPath(setup.Organization.Id, Guid.NewGuid()),
            factory.RequestCancellationToken);
        var foreignBody = RemoveCorrelationIdentifiers(
            await foreignResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var missingBody = RemoveCorrelationIdentifiers(
            await missingResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(foreignResponse.Headers.ToString(), missingResponse.Headers.ToString());
        Assert.Equal(
            foreignResponse.Content.Headers.ToString(),
            missingResponse.Content.Headers.ToString());
        Assert.Equal(foreignBody, missingBody);
    }

    [Fact]
    public async Task ConcurrentOverconsumptionCreatesExactlyOneEvent()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var input = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        await using var lotBlocker = await OpenConnectionAsync();
        await using var lotBlockerTransaction = await lotBlocker.BeginTransactionAsync();
        await ExecuteNonQueryAsync(
            lotBlocker,
            lotBlockerTransaction,
            "LOCK TABLE trace.lot IN ACCESS EXCLUSIVE MODE");
        await using var inputBlocker = await OpenConnectionAsync();
        await using var inputBlockerTransaction = await inputBlocker.BeginTransactionAsync();
        await ExecuteNonQueryAsync(
            inputBlocker,
            inputBlockerTransaction,
            "LOCK TABLE trace.event_input IN ACCESS EXCLUSIVE MODE");

        var request = ValidRequest(
            setup.Organization.LocationId,
            [new(input.Id, 6m)],
            []);
        var firstRequest = client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            request,
            factory.RequestCancellationToken);
        var secondRequest = client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            request,
            factory.RequestCancellationToken);
        var lotLockReleased = false;
        var inputLockReleased = false;

        try
        {
            await WaitForBlockedWriterQueriesAsync(
                minimumBlockedQueries: 2,
                minimumInputQueries: 0,
                factory.RequestCancellationToken);
            await lotBlockerTransaction.CommitAsync(factory.RequestCancellationToken);
            lotLockReleased = true;

            // With FOR UPDATE, one request waits on the lot row while the other waits on
            // event_input. Without it, both reach event_input. Requiring two blocked writer
            // queries and at least one input query makes both paths deterministic.
            await WaitForBlockedWriterQueriesAsync(
                minimumBlockedQueries: 2,
                minimumInputQueries: 1,
                factory.RequestCancellationToken);
            await inputBlockerTransaction.CommitAsync(factory.RequestCancellationToken);
            inputLockReleased = true;
        }
        finally
        {
            if (!lotLockReleased)
            {
                await lotBlockerTransaction.RollbackAsync(CancellationToken.None);
            }

            if (!inputLockReleased)
            {
                await inputBlockerTransaction.RollbackAsync(CancellationToken.None);
            }
        }

        using var firstResponse = await firstRequest;
        using var secondResponse = await secondRequest;
        var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Conflict));

        await using var context = database.CreateLotApiTraceabilityDbContext();
        var persistedInputs = await context.Set<EventInput>()
            .AsNoTracking()
            .Where(line => line.LotId == input.Id)
            .Select(line => new
            {
                EventId = EF.Property<Guid>(line, "EventId"),
                line.Quantity,
            })
            .ToListAsync();
        var persistedInput = Assert.Single(persistedInputs);
        Assert.Equal(1, await context.TraceabilityEvents.CountAsync(
            traceabilityEvent => traceabilityEvent.Id == persistedInput.EventId));
        Assert.True(persistedInputs.Sum(line => line.Quantity) <= input.Quantity);
        var persistedLotQuantity = await context.Lots
            .Where(lot => lot.Id == input.Id)
            .Select(lot => lot.Quantity)
            .SingleAsync();
        Assert.Equal(10m, persistedLotQuantity);
    }

    [Fact]
    public Task DirectCycleReturns409() => AssertCycleRejectedAsync(2);

    [Fact]
    public Task LongerCycleReturns409() => AssertCycleRejectedAsync(3);

    private async Task AssertCycleRejectedAsync(int lotCount)
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lots = new List<Lot>();
        for (var index = 0; index < lotCount; index++)
        {
            lots.Add(await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m));
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        for (var index = 0; index < lots.Count - 1; index++)
        {
            using var response = await PostEdgeAsync(
                client, setup, lots[index], lots[index + 1], cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var rejected = await PostEdgeAsync(
            client, setup, lots[^1], lots[0], cancellationToken);
        using var problem = await AssertProblemAsync(
            rejected, HttpStatusCode.Conflict, "TRACEABILITY_EVENT_CONFLICT", cancellationToken);
        Assert.Contains("cycle", problem.RootElement.GetProperty("detail").GetString());

        await AssertPersistedGraphIsAcyclicAsync(setup.Organization.Id, lotCount - 1);
    }

    [Fact]
    public async Task SharedAncestryDiamondReturns201()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var a = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var b = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var c = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var d = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);

        using var first = await PostEdgeAsync(client, setup, a, b, cancellationToken);
        using var second = await PostEdgeAsync(client, setup, a, c, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var merge = await client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(setup.Organization.LocationId,
                [new(b.Id, 1m), new(c.Id, 1m)], [new(d.Id, 2m)]),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, merge.StatusCode);
        await AssertPersistedGraphIsAcyclicAsync(setup.Organization.Id, 3);
    }

    [Fact]
    public async Task LotCanBeUsedInMultipleIndependentEvents()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var a = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var b = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var c = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);

        using var first = await PostEdgeAsync(client, setup, a, c, cancellationToken);
        using var second = await PostEdgeAsync(client, setup, b, c, cancellationToken);
        using var third = await PostEdgeAsync(client, setup, a, c, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        await AssertPersistedGraphIsAcyclicAsync(setup.Organization.Id, 3);
    }

    [Fact]
    public async Task SameLotOnBothSidesReturns400()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var lot = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);

        using var response = await PostEdgeAsync(client, setup, lot, lot, cancellationToken);
        using var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "TRACEABILITY_EVENT_VALIDATION_FAILED",
            cancellationToken);
        Assert.Contains(lot.Id.ToString(), problem.RootElement.GetProperty("detail").GetString());
        await AssertPersistedGraphIsAcyclicAsync(setup.Organization.Id, 0);
    }

    [Fact]
    public async Task ConcurrentDisjointEventsCreateExactlyOneEventWithoutCycle()
    {
        var setup = await CreateAuthorizedSetupAsync(StandardRoleIds.Producer);
        var x = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var y = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var p = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        var q = await CreateLotAsync(setup.Organization.Id, setup.Article.Id, 10m);
        Assert.Equal(4, new[] { x.Id, y.Id, p.Id, q.Id }.Distinct().Count());
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var cancellationToken = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, cancellationToken);
        using var xy = await PostEdgeAsync(client, setup, x, y, cancellationToken);
        using var pq = await PostEdgeAsync(client, setup, p, q, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, xy.StatusCode);
        Assert.Equal(HttpStatusCode.Created, pq.StatusCode);

        await using var lotBlocker = await OpenConnectionAsync();
        await using var lotTransaction = await lotBlocker.BeginTransactionAsync(cancellationToken);
        await ExecuteNonQueryAsync(lotBlocker, lotTransaction,
            "LOCK TABLE trace.lot IN ACCESS EXCLUSIVE MODE");
        await using var eventBlocker = await OpenConnectionAsync();
        await using var eventTransaction = await eventBlocker.BeginTransactionAsync(cancellationToken);
        // SHARE permits the ancestry reads but stops INSERT after the cycle check.
        await ExecuteNonQueryAsync(eventBlocker, eventTransaction,
            "LOCK TABLE trace.traceability_event IN SHARE MODE");

        var firstRequest = PostEdgeAsync(client, setup, y, p, cancellationToken);
        var secondRequest = PostEdgeAsync(client, setup, q, x, cancellationToken);
        var lotLockReleased = false;
        var eventLockReleased = false;
        try
        {
            // Both requests must be demonstrably blocked, directly or through the writer
            // holding the advisory lock. No elapsed delay is used as proof of readiness.
            await WaitForBlockedWriterChainAsync(lotBlocker.ProcessID, cancellationToken);
            await lotTransaction.CommitAsync(cancellationToken);
            lotLockReleased = true;

            // With the advisory lock: one INSERT waits here, the other writer waits on it.
            // Without it: both disjoint writers finish their cycle checks and wait at INSERT.
            // Releasing only after observing both makes the missing-lock mutation fail
            // deterministically: neither check can see the other's uncommitted edge.
            await WaitForBlockedWriterChainAsync(eventBlocker.ProcessID, cancellationToken);
            await eventTransaction.CommitAsync(cancellationToken);
            eventLockReleased = true;
        }
        finally
        {
            if (!lotLockReleased)
            {
                await lotTransaction.RollbackAsync(CancellationToken.None);
            }

            if (!eventLockReleased)
            {
                await eventTransaction.RollbackAsync(CancellationToken.None);
            }
        }

        using var firstResponse = await firstRequest;
        using var secondResponse = await secondRequest;
        var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Conflict));
        using var problem = await AssertProblemAsync(
            firstResponse.StatusCode == HttpStatusCode.Conflict ? firstResponse : secondResponse,
            HttpStatusCode.Conflict, "TRACEABILITY_EVENT_CONFLICT", cancellationToken);
        Assert.Contains("cycle", problem.RootElement.GetProperty("detail").GetString());
        await AssertPersistedGraphIsAcyclicAsync(setup.Organization.Id, 3);
    }

    private static Task<HttpResponseMessage> PostEdgeAsync(
        HttpClient client, AuthorizedSetup setup, Lot input, Lot output,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            EventCollectionPath(setup.Organization.Id),
            ValidRequest(setup.Organization.LocationId,
                [new(input.Id, 1m)], [new(output.Id, 1m)]),
            cancellationToken);

    private async Task WaitForBlockedWriterChainAsync(
        int blockerPid, CancellationToken cancellationToken)
    {
        await using var monitor = await OpenConnectionAsync();
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < DatabaseStateTimeout)
        {
            await using var command = monitor.CreateCommand();
            command.CommandText =
                """
                WITH RECURSIVE waiting(pid) AS (
                    SELECT pid FROM pg_stat_activity
                     WHERE datname = current_database()
                       AND @blocker = ANY(pg_blocking_pids(pid))
                    UNION
                    SELECT a.pid FROM pg_stat_activity a
                      JOIN waiting w ON w.pid = ANY(pg_blocking_pids(a.pid))
                     WHERE a.datname = current_database()
                )
                SELECT count(*) FROM waiting
                """;
            command.Parameters.AddWithValue("blocker", blockerPid);
            if ((long)(await command.ExecuteScalarAsync(cancellationToken))! == 2)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        throw new TimeoutException(
            "Both disjoint event writers did not reach the controlled database lock barrier.");
    }

    private async Task AssertPersistedGraphIsAcyclicAsync(Guid organizationId, int expectedEvents)
    {
        await using var context = database.CreateLotApiTraceabilityDbContext();
        var events = await context.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(traceabilityEvent => traceabilityEvent.Inputs)
            .Include(traceabilityEvent => traceabilityEvent.Outputs)
            .Where(traceabilityEvent => traceabilityEvent.OrganizationId == organizationId)
            .ToListAsync();
        Assert.Equal(expectedEvents, events.Count);

        // Independent in-memory check of all persisted input/output pairs, not a repeat
        // of the writer's ancestor SQL. A path must never return to its starting lot.
        var edges = events.SelectMany(traceabilityEvent => traceabilityEvent.Inputs
            .SelectMany(input => traceabilityEvent.Outputs.Select(output =>
                (Parent: input.LotId, Child: output.LotId)))).ToLookup(edge => edge.Parent);
        foreach (var start in edges.Select(group => group.Key))
        {
            var visited = new HashSet<Guid>();
            var pending = new Queue<Guid>(edges[start].Select(edge => edge.Child));
            while (pending.TryDequeue(out var current))
            {
                Assert.NotEqual(start, current);
                if (visited.Add(current))
                {
                    foreach (var edge in edges[current])
                    {
                        pending.Enqueue(edge.Child);
                    }
                }
            }
        }
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

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
        var email = $"event-api-{userId:N}@example.com";
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

    private async Task<Lot> CreateLotAsync(
        Guid organizationId,
        Guid articleId,
        decimal quantity)
    {
        var lot = Lot.Create(
            Guid.NewGuid(),
            organizationId,
            articleId,
            $"EVENT-LOT-{Guid.NewGuid():N}",
            quantity,
            KilogramId,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return lot;
    }

    private async Task<TraceabilityEvent> CreatePersistedEventAsync(
        Guid createdBy,
        TestOrganization organization,
        Lot inputLot)
    {
        var now = DateTimeOffset.UtcNow;
        var traceabilityEvent = TraceabilityEvent.Create(
            Guid.NewGuid(),
            PressId,
            organization.Id,
            organization.LocationId,
            now,
            externalReference: null,
            description: null,
            createdBy,
            now,
            [EventInput.Create(Guid.NewGuid(), inputLot.Id, 1m, inputLot.UnitId)],
            []);
        await using var context = database.CreateLotApiTraceabilityDbContext();
        context.TraceabilityEvents.Add(traceabilityEvent);
        await context.SaveChangesAsync();
        return traceabilityEvent;
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

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(database.LotApiConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task WaitForBlockedWriterQueriesAsync(
        int minimumBlockedQueries,
        int minimumInputQueries,
        CancellationToken cancellationToken)
    {
        await using var monitor = await OpenConnectionAsync();
        var startedAt = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(startedAt) < DatabaseStateTimeout)
        {
            await using var command = monitor.CreateCommand();
            command.CommandText =
                """
                SELECT
                    count(*) FILTER (
                        WHERE query LIKE '%event_input%') AS input_queries,
                    count(*) AS blocked_queries
                  FROM pg_stat_activity
                 WHERE datname = current_database()
                   AND state = 'active'
                   AND wait_event_type = 'Lock'
                   AND (query LIKE '%FROM trace.lot%'
                        OR query LIKE '%event_input%'
                        OR query LIKE '%pg_advisory_xact_lock%')
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            Assert.True(await reader.ReadAsync(cancellationToken));
            var inputQueries = reader.GetInt64(0);
            var blockedQueries = reader.GetInt64(1);
            if (blockedQueries >= minimumBlockedQueries
                && inputQueries >= minimumInputQueries)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        throw new TimeoutException(
            "Concurrent event writers did not reach the expected blocked database state.");
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
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

    private static async Task<TraceabilityEventTestResponse> ReadEventAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<TraceabilityEventTestResponse>(
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The traceability event response body was empty.");
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
            ?? throw new InvalidOperationException(
                "The problem details response body was empty.");
        Assert.True(problemDetails.Remove("correlationId"));
        Assert.True(problemDetails.Remove("traceId"));
        return problemDetails.ToJsonString();
    }

    private static CreateTraceabilityEventTestRequest ValidRequest(
        Guid locationId,
        IReadOnlyList<TraceabilityEventLotTestRequest> inputs,
        IReadOnlyList<TraceabilityEventLotTestRequest> outputs) =>
        new(
            PressCode,
            locationId,
            new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero),
            "EXT-TRC-008",
            "Traceability event endpoint test",
            inputs,
            outputs);

    private static string EventCollectionPath(Guid organizationId) =>
        $"/api/v1/organizations/{organizationId}/traceability/events";

    private static string EventPath(Guid organizationId, Guid eventId) =>
        $"{EventCollectionPath(organizationId)}/{eventId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TestOrganization(Guid Id, Guid LocationId);

    private sealed record AuthorizedSetup(
        TestAccount Account,
        TestOrganization Organization,
        Product Product,
        Article Article);

    private sealed record TokenResponse(string AccessToken);

    private sealed record CreateTraceabilityEventTestRequest(
        string EventTypeCode,
        Guid LocationId,
        DateTimeOffset OccurredAt,
        string? ExternalReference,
        string? Description,
        IReadOnlyList<TraceabilityEventLotTestRequest> Inputs,
        IReadOnlyList<TraceabilityEventLotTestRequest> Outputs);

    private sealed record TraceabilityEventLotTestRequest(Guid LotId, decimal Quantity);

    private sealed record TraceabilityEventTestResponse(
        Guid Id,
        Guid OrganizationId,
        string EventTypeCode,
        Guid LocationId,
        DateTimeOffset OccurredAt,
        string? ExternalReference,
        string? Description,
        Guid CreatedBy,
        DateTimeOffset CreatedAt,
        IReadOnlyList<TraceabilityEventLotTestResponse> Inputs,
        IReadOnlyList<TraceabilityEventLotTestResponse> Outputs);

    private sealed record TraceabilityEventLotTestResponse(
        Guid LotId,
        decimal Quantity,
        Guid UnitId);
}

public sealed class TraceabilityEventRequestContractTests
{
    [Fact]
    public void RequestLinesContainNoUnitMember()
    {
        var propertyNames = typeof(TraceabilityEventLotRequest)
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public)
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "LotId", "Quantity" }, propertyNames);
    }
}
