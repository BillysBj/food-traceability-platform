using FoodTraceability.Modules.Traceability.Application.Events;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateTraceabilityEventServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid EventTypeId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly TestIdentifiers TestIds = new();
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task MultipleInputsAndOutputsUseUnitsFromReferencedLots()
    {
        var inputOne = new TraceabilityEventLotCommand(TestIds.LotOne, 4m);
        var inputTwo = new TraceabilityEventLotCommand(TestIds.LotTwo, 5m);
        var outputOne = new TraceabilityEventLotCommand(TestIds.LotThree, 6m);
        var outputTwo = new TraceabilityEventLotCommand(TestIds.LotFour, 7m);
        var writer = new StubTraceabilityEventWriter(new Dictionary<Guid, ReferencedLotDetails>
        {
            [TestIds.LotOne] = new(TestIds.LotOne, 10m, TestIds.UnitOne),
            [TestIds.LotTwo] = new(TestIds.LotTwo, 10m, TestIds.UnitTwo),
            [TestIds.LotThree] = new(TestIds.LotThree, 10m, TestIds.UnitThree),
            [TestIds.LotFour] = new(TestIds.LotFour, 10m, TestIds.UnitFour),
        });
        var service = CreateService(writer);

        var result = await service.CreateAsync(
            ValidCommand([inputOne, inputTwo], [outputOne, outputTwo]),
            CancellationToken.None);

        Assert.Equal(OrganizationId, result.OrganizationId);
        Assert.Equal(EventTypeId, result.EventTypeId);
        Assert.Equal(LocationId, result.LocationId);
        Assert.Equal(OccurredAt, result.OccurredAt);
        Assert.Equal(CreatedBy, result.CreatedBy);
        Assert.Equal(CreatedAt, result.CreatedAt);
        Assert.Equal(
            new[] { TestIds.UnitOne, TestIds.UnitTwo },
            result.Inputs.Select(input => input.UnitId));
        Assert.Equal(
            new[] { TestIds.UnitThree, TestIds.UnitFour },
            result.Outputs.Select(output => output.UnitId));
        Assert.NotNull(writer.NewEvent);
    }

    [Fact]
    public async Task EventWithoutInputsOrOutputsIsRejectedBeforeWriting()
    {
        var writer = new StubTraceabilityEventWriter(
            new Dictionary<Guid, ReferencedLotDetails>());
        var service = CreateService(writer);

        var exception = await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            service.CreateAsync(ValidCommand([], []), CancellationToken.None));

        Assert.Equal(
            "Traceability event must have at least one input or output.",
            exception.Message);
        Assert.Null(writer.NewEvent);
    }

    [Fact]
    public async Task DuplicateInputLotDomainViolationBecomesValidationException()
    {
        var writer = new StubTraceabilityEventWriter(new Dictionary<Guid, ReferencedLotDetails>
        {
            [TestIds.LotOne] = new(TestIds.LotOne, 10m, TestIds.UnitOne),
        });
        var service = CreateService(writer);
        var command = ValidCommand(
            [
                new TraceabilityEventLotCommand(TestIds.LotOne, 2m),
                new TraceabilityEventLotCommand(TestIds.LotOne, 2m),
            ],
            []);

        var exception = await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            service.CreateAsync(command, CancellationToken.None));

        Assert.Equal(
            "Traceability event inputs must not contain duplicate lot ids.",
            exception.Message);
    }

    [Fact]
    public async Task QuantityWithMoreThanSixDecimalPlacesIsRejectedBeforeWriting()
    {
        var writer = new StubTraceabilityEventWriter(
            new Dictionary<Guid, ReferencedLotDetails>());
        var service = CreateService(writer);
        var command = ValidCommand(
            [new TraceabilityEventLotCommand(TestIds.LotOne, 1.0000005m)],
            []);

        var exception = await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            service.CreateAsync(command, CancellationToken.None));

        Assert.Equal(
            "Traceability event input quantity must not have more than 6 decimal places.",
            exception.Message);
        Assert.Null(writer.NewEvent);
    }

    [Fact]
    public async Task DomainQuantityViolationBecomesValidationException()
    {
        var writer = new StubTraceabilityEventWriter(new Dictionary<Guid, ReferencedLotDetails>
        {
            [TestIds.LotOne] = new(TestIds.LotOne, 10m, TestIds.UnitOne),
        });
        var service = CreateService(writer);

        var exception = await Assert.ThrowsAsync<TraceabilityEventValidationException>(() =>
            service.CreateAsync(
                ValidCommand(
                    [new TraceabilityEventLotCommand(TestIds.LotOne, 0m)],
                    []),
                CancellationToken.None));

        Assert.Equal("Event input quantity must be greater than zero.", exception.Message);
    }

    private static CreateTraceabilityEventService CreateService(
        ITraceabilityEventWriter writer) =>
        new(writer, new FixedTimeProvider(CreatedAt));

    private static CreateTraceabilityEventCommand ValidCommand(
        IReadOnlyList<TraceabilityEventLotCommand>? inputs,
        IReadOnlyList<TraceabilityEventLotCommand>? outputs) =>
        new(
            OrganizationId,
            EventTypeId,
            LocationId,
            OccurredAt,
            " EXT-123 ",
            " Pressing ",
            CreatedBy,
            inputs,
            outputs);

    private sealed class StubTraceabilityEventWriter(
        IReadOnlyDictionary<Guid, ReferencedLotDetails> referencedLots)
        : ITraceabilityEventWriter
    {
        public NewTraceabilityEvent? NewEvent { get; private set; }

        public Task<TraceabilityEvent> AddAsync(
            NewTraceabilityEvent newEvent,
            BuildTraceabilityEvent buildEvent,
            CancellationToken cancellationToken)
        {
            NewEvent = newEvent;
            return Task.FromResult(buildEvent(referencedLots));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestIdentifiers
    {
        public Guid LotOne { get; } = Guid.NewGuid();

        public Guid LotTwo { get; } = Guid.NewGuid();

        public Guid LotThree { get; } = Guid.NewGuid();

        public Guid LotFour { get; } = Guid.NewGuid();

        public Guid UnitOne { get; } = Guid.NewGuid();

        public Guid UnitTwo { get; } = Guid.NewGuid();

        public Guid UnitThree { get; } = Guid.NewGuid();

        public Guid UnitFour { get; } = Guid.NewGuid();
    }
}
