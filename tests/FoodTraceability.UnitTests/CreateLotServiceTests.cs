using FoodTraceability.Modules.Traceability.Application.Lots;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateLotServiceTests
{
    [Fact]
    public async Task ValidCommandCreatesTrimmedLotDetails()
    {
        var organizationId = Guid.NewGuid();
        var articleId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var writer = new StubLotWriter();
        var service = new CreateLotService(writer, new FixedTimeProvider(now));

        var result = await service.CreateAsync(
            new CreateLotCommand(
                organizationId,
                articleId,
                "  LOT-001  ",
                12.345678m,
                unitId),
            CancellationToken.None);

        var persisted = Assert.IsType<Lot>(writer.Lot);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(articleId, result.ArticleId);
        Assert.Equal("LOT-001", result.LotNumber);
        Assert.Equal(12.345678m, result.Quantity);
        Assert.Equal(unitId, result.UnitId);
        Assert.Equal(now, result.CreatedAt);
    }

    [Fact]
    public async Task QuantityWithPrecisionLossBeyondSixDecimalPlacesIsRejected()
    {
        var exception = await AssertInvalidQuantityAsync(1.0000005m);

        Assert.Equal(
            "Quantity must not have more than 6 decimal places.",
            exception.Message);
    }

    [Fact]
    public async Task ScaleSevenWithoutPrecisionLossIsAccepted()
    {
        var writer = new StubLotWriter();
        var service = CreateService(writer);

        var result = await service.CreateAsync(ValidCommand(1.1000000m), CancellationToken.None);

        Assert.Equal(1.1000000m, result.Quantity);
        Assert.NotNull(writer.Lot);
    }

    [Fact]
    public async Task SmallestSixDecimalPlaceQuantityIsAccepted()
    {
        var writer = new StubLotWriter();
        var service = CreateService(writer);

        var result = await service.CreateAsync(ValidCommand(0.000001m), CancellationToken.None);

        Assert.Equal(0.000001m, result.Quantity);
        Assert.NotNull(writer.Lot);
    }

    [Fact]
    public async Task QuantityOutsideNumericRangeIsRejected()
    {
        var exception = await AssertInvalidQuantityAsync(1000000000000m);

        Assert.Equal("Quantity exceeds the supported range.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveQuantityIsRejected(int quantity)
    {
        var exception = await AssertInvalidQuantityAsync(quantity);

        Assert.Equal("Lot quantity must be greater than zero.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyLotNumberIsRejected(string? lotNumber)
    {
        var writer = new StubLotWriter();
        var service = CreateService(writer);

        var exception = await Assert.ThrowsAsync<LotValidationException>(() =>
            service.CreateAsync(
                ValidCommand(1m) with { LotNumber = lotNumber },
                CancellationToken.None));

        Assert.Equal(
            "Lot number must not be null, empty, or consist only of whitespace.",
            exception.Message);
        Assert.Null(writer.Lot);
    }

    [Fact]
    public async Task LotNumberOverMaximumAfterTrimmingIsRejected()
    {
        var writer = new StubLotWriter();
        var service = CreateService(writer);
        var lotNumber = $"  {new string('A', Lot.MaximumLotNumberLength + 1)}  ";

        var exception = await Assert.ThrowsAsync<LotValidationException>(() =>
            service.CreateAsync(
                ValidCommand(1m) with { LotNumber = lotNumber },
                CancellationToken.None));

        Assert.Equal(
            $"Lot number must not exceed {Lot.MaximumLotNumberLength} characters.",
            exception.Message);
        Assert.Null(writer.Lot);
    }

    private static CreateLotService CreateService(ILotWriter writer) =>
        new(writer, TimeProvider.System);

    private static CreateLotCommand ValidCommand(decimal quantity) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LOT-001",
            quantity,
            Guid.NewGuid());

    private static async Task<LotValidationException> AssertInvalidQuantityAsync(
        decimal quantity)
    {
        var writer = new StubLotWriter();
        var service = CreateService(writer);

        var exception = await Assert.ThrowsAsync<LotValidationException>(() =>
            service.CreateAsync(ValidCommand(quantity), CancellationToken.None));

        Assert.Null(writer.Lot);
        return exception;
    }

    private sealed class StubLotWriter : ILotWriter
    {
        public Lot? Lot { get; private set; }

        public Task AddAsync(Lot lot, CancellationToken cancellationToken)
        {
            Lot = lot;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
