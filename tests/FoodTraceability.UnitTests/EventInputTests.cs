using System.Globalization;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EventInputTests
{
    private static readonly Guid EventInputId =
        Guid.Parse("55965257-c638-48f0-b633-af15674597bd");
    private static readonly Guid LotId =
        Guid.Parse("87a5b834-b962-47f9-8c8d-4bd1aa35196c");
    private static readonly Guid UnitId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private const decimal Quantity = 125.75m;

    [Fact]
    public void ValidEventInputIsCreatedWithProvidedValues()
    {
        var input = Create();

        Assert.Equal(EventInputId, input.Id);
        Assert.Equal(LotId, input.LotId);
        Assert.Equal(Quantity, input.Quantity);
        Assert.Equal(UnitId, input.UnitId);
    }

    [Fact]
    public void EmptyEventInputIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => Create(id: Guid.Empty));

        Assert.Contains("input id", exception.Message);
    }

    [Fact]
    public void EmptyLotIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => Create(lotId: Guid.Empty));

        Assert.Contains("lot id", exception.Message);
    }

    [Fact]
    public void EmptyUnitIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => Create(unitId: Guid.Empty));

        Assert.Contains("unit id", exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.000001")]
    public void NonPositiveQuantityIsRejected(string quantity)
    {
        var parsedQuantity = decimal.Parse(quantity, CultureInfo.InvariantCulture);

        Assert.Throws<TraceabilityDomainException>(() => Create(quantity: parsedQuantity));
    }

    [Fact]
    public void SmallestRepresentableQuantityIsAccepted()
    {
        var input = Create(quantity: 0.000001m);

        Assert.Equal(0.000001m, input.Quantity);
    }

    private static EventInput Create(
        Guid? id = null,
        Guid? lotId = null,
        decimal? quantity = null,
        Guid? unitId = null)
    {
        return EventInput.Create(
            id ?? EventInputId,
            lotId ?? LotId,
            quantity ?? Quantity,
            unitId ?? UnitId);
    }
}
