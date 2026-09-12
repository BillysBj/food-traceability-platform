using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Contracts.Traceability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModuleEvents = FoodTraceability.Modules.Traceability.Application.Events;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class TraceabilityEventCreatorContractTests(PostgreSqlContainerFixture database)
{
    private static readonly Guid PressId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");
    private static readonly Guid KilogramId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    [Fact]
    public void CreatorResolvesFromApplicationScope()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>());
    }

    [Fact]
    public async Task CreatePersistsEventAndAllLinesAndReturnsStoredOccurrenceTime()
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var request = await CreateRequestAsync(factory);
        CreateTraceabilityEventResult result;

        using (var scope = factory.Services.CreateScope())
        {
            var creator = scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();
            result = await creator.CreateAsync(request, cancellationToken);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var persisted = await context.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(traceabilityEvent => traceabilityEvent.Inputs)
            .Include(traceabilityEvent => traceabilityEvent.Outputs)
            .SingleAsync(traceabilityEvent => traceabilityEvent.Id == result.EventId, cancellationToken);

        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.Equal(request.OrganizationId, persisted.OrganizationId);
        Assert.Equal(PressId, persisted.EventTypeId);
        Assert.Equal(request.LocationId, persisted.LocationId);
        Assert.Equal(request.ExternalReference, persisted.ExternalReference);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Equal(request.CreatedBy, persisted.CreatedBy);
        Assert.Equal(request.OccurredAt.UtcTicks - 7, result.OccurredAt.UtcTicks);
        Assert.Equal(persisted.OccurredAt.UtcTicks, result.OccurredAt.UtcTicks);
        Assert.Equal(
            request.Inputs!.OrderBy(line => line.LotId),
            persisted.Inputs.Select(line => new TraceabilityEventLot(line.LotId, line.Quantity))
                .OrderBy(line => line.LotId));
        Assert.Equal(
            request.Outputs!.OrderBy(line => line.LotId),
            persisted.Outputs.Select(line => new TraceabilityEventLot(line.LotId, line.Quantity))
                .OrderBy(line => line.LotId));
        Assert.All(persisted.Inputs, line => Assert.Equal(KilogramId, line.UnitId));
        Assert.All(persisted.Outputs, line => Assert.Equal(KilogramId, line.UnitId));
    }

    [Theory]
    [InlineData("NOT_A_REGISTERED_EVENT", false, "The referenced event type does not exist.")]
    [InlineData("NOT_A_REGISTERED_EVENT", true, "The referenced event type does not exist.")]
    [InlineData("QUALITY_RELEASE", false, "Event type 'QUALITY_RELEASE' is not allowed for traceability events.")]
    [InlineData("QUALITY_RELEASE", true, "Event type 'QUALITY_RELEASE' is not allowed for traceability events.")]
    [InlineData(" quality_release ", false, "Event type ' quality_release ' is not allowed for traceability events.")]
    public async Task InvalidEventTypeCodeThrowsContractValidationExceptionBeforeQuantityValidation(
        string eventTypeCode,
        bool invalidQuantity,
        string expectedMessage)
    {
        await using var factory = CreateFactory();
        var request = (await CreateRequestAsync(factory)) with { EventTypeCode = eventTypeCode };
        if (invalidQuantity)
        {
            request = request with { Inputs = [new(request.Inputs![0].LotId, 0m)] };
        }

        using var scope = factory.Services.CreateScope();
        var creator = scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();

        var exception = await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            creator.CreateAsync(request, factory.RequestCancellationToken));

        Assert.Equal(expectedMessage, exception.Message);
        var context = scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        Assert.False(await context.TraceabilityEvents.AnyAsync(
            traceabilityEvent => traceabilityEvent.OrganizationId == request.OrganizationId,
            factory.RequestCancellationToken));
    }

    [Fact]
    public async Task SampleCodeCreatesEventThroughContract()
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var request = (await CreateRequestAsync(factory)) with { EventTypeCode = "SAMPLE", Outputs = [] };
        CreateTraceabilityEventResult result;

        using (var scope = factory.Services.CreateScope())
        {
            var creator = scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();
            result = await creator.CreateAsync(request, cancellationToken);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var persisted = await context.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(traceabilityEvent => traceabilityEvent.Inputs)
            .Include(traceabilityEvent => traceabilityEvent.Outputs)
            .SingleAsync(traceabilityEvent => traceabilityEvent.Id == result.EventId, cancellationToken);
        var eventType = await context.EventTypes.AsNoTracking()
            .SingleAsync(eventType => eventType.Id == persisted.EventTypeId, cancellationToken);

        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.Equal("SAMPLE", eventType.Code.Value);
        Assert.Equal(request.OrganizationId, persisted.OrganizationId);
        Assert.Equal(
            request.Inputs!.OrderBy(line => line.LotId),
            persisted.Inputs.Select(line => new TraceabilityEventLot(line.LotId, line.Quantity))
                .OrderBy(line => line.LotId));
        Assert.Empty(persisted.Outputs);
    }

    [Fact]
    public async Task EmptyInputsAndOutputsThrowContractValidationException()
    {
        await using var factory = CreateFactory();
        var request = (await CreateRequestAsync(factory)) with { Inputs = [], Outputs = [] };
        using var scope = factory.Services.CreateScope();
        var creator = scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();

        await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            creator.CreateAsync(request, factory.RequestCancellationToken));
    }

    [Fact]
    public async Task OverconsumptionThrowsContractConflictException()
    {
        await using var factory = CreateFactory();
        var request = Overconsume(await CreateRequestAsync(factory));
        using var scope = factory.Services.CreateScope();
        var creator = scope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();

        await Assert.ThrowsAsync<TraceabilityEventConflictException>(() =>
            creator.CreateAsync(request, factory.RequestCancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContractExceptionPreservesOriginalModuleMessage(bool conflict)
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var validRequest = await CreateRequestAsync(factory);
        var request = conflict
            ? Overconsume(validRequest)
            : validRequest with { Inputs = [], Outputs = [] };
        Exception original;

        using (var originalScope = factory.Services.CreateScope())
        {
            var service = originalScope.ServiceProvider
                .GetRequiredService<ModuleEvents.CreateTraceabilityEventService>();
            var command = new ModuleEvents.CreateTraceabilityEventCommand(
                request.OrganizationId,
                request.EventTypeCode,
                request.LocationId,
                request.OccurredAt,
                request.ExternalReference,
                request.Description,
                request.CreatedBy,
                request.Inputs!.Select(line =>
                    new ModuleEvents.TraceabilityEventLotCommand(line.LotId, line.Quantity)).ToArray(),
                request.Outputs!.Select(line =>
                    new ModuleEvents.TraceabilityEventLotCommand(line.LotId, line.Quantity)).ToArray());
            original = conflict
                ? await Assert.ThrowsAsync<ModuleEvents.TraceabilityEventConflictException>(() =>
                    service.CreateAsync(command, cancellationToken))
                : await Assert.ThrowsAsync<ModuleEvents.TraceabilityEventValidationException>(() =>
                    service.CreateAsync(command, cancellationToken));
        }

        using var contractScope = factory.Services.CreateScope();
        var creator = contractScope.ServiceProvider.GetRequiredService<ITraceabilityEventCreator>();
        Exception translated = conflict
            ? await Assert.ThrowsAsync<TraceabilityEventConflictException>(() =>
                creator.CreateAsync(request, cancellationToken))
            : await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
                creator.CreateAsync(request, cancellationToken));

        Assert.Equal(original.Message, translated.Message);
    }

    private static CreateTraceabilityEventRequest Overconsume(CreateTraceabilityEventRequest request) =>
        request with { Inputs = [new(request.Inputs![0].LotId, 11m)] };

    private static async Task<CreateTraceabilityEventRequest> CreateRequestAsync(
        ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var cancellationToken = factory.RequestCancellationToken;
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var organizations = services.GetRequiredService<OrganizationsDbContext>();
        organizations.Organizations.Add(Organization.Create(
            organizationId,
            $"Contract organization {organizationId:N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            now));
        organizations.Locations.Add(Location.Create(
            locationId,
            organizationId,
            "Contract location",
            city: null,
            region: null,
            countryCode: null,
            latitude: null,
            longitude: null,
            now));
        await organizations.SaveChangesAsync(cancellationToken);

        var identity = services.GetRequiredService<IdentityDbContext>();
        identity.Users.Add(User.Create(
            userId,
            EmailAddress.Create($"contract-{userId:N}@example.com"),
            "Contract",
            "Test",
            now));
        await identity.SaveChangesAsync(cancellationToken);

        var catalog = services.GetRequiredService<CatalogDbContext>();
        catalog.Products.Add(Product.Create(productId, $"CONTRACT-{productId:N}", "Contract product", now));
        catalog.Articles.Add(Article.Create(
            articleId, organizationId, productId, $"CONTRACT-{articleId:N}", gtin: null, now));
        await catalog.SaveChangesAsync(cancellationToken);

        var traceability = services.GetRequiredService<TraceabilityDbContext>();
        var firstInput = Lot.Create(
            Guid.NewGuid(), organizationId, articleId, "INPUT-1", 10m, KilogramId, now);
        var secondInput = Lot.Create(
            Guid.NewGuid(), organizationId, articleId, "INPUT-2", 20m, KilogramId, now);
        var output = Lot.Create(
            Guid.NewGuid(), organizationId, articleId, "OUTPUT", 25m, KilogramId, now);
        traceability.Lots.AddRange(firstInput, secondInput, output);
        await traceability.SaveChangesAsync(cancellationToken);

        return new CreateTraceabilityEventRequest(
            organizationId,
            "PRESS",
            locationId,
            new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_567),
            $"CONTRACT-EVENT-{Guid.NewGuid():N}",
            "Contract event description",
            userId,
            [new(firstInput.Id, 7m), new(secondInput.Id, 18m)],
            [new(output.Id, 25m)]);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
        });
}
