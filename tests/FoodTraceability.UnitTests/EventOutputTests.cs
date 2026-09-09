using System.Globalization;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EventOutputTests
{
    private static readonly Guid EventOutputId =
        Guid.Parse("06f23062-5e52-4650-90d6-d47e50212e13");
    private static readonly Guid LotId =
        Guid.Parse("0ee56e33-ef2d-4849-a881-1539ec661bab");
    private static readonly Guid UnitId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private const decimal Quantity = 98.25m;

    [Fact]
    public void ValidEventOutputIsCreatedWithProvidedValues()
    {
        var output = Create();

        Assert.Equal(EventOutputId, output.Id);
        Assert.Equal(LotId, output.LotId);
        Assert.Equal(Quantity, output.Quantity);
        Assert.Equal(UnitId, output.UnitId);
    }

    [Fact]
    public void EmptyEventOutputIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => Create(id: Guid.Empty));

        Assert.Contains("output id", exception.Message);
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
        var output = Create(quantity: 0.000001m);

        Assert.Equal(0.000001m, output.Quantity);
    }

    private static EventOutput Create(
        Guid? id = null,
        Guid? lotId = null,
        decimal? quantity = null,
        Guid? unitId = null)
    {
        return EventOutput.Create(
            id ?? EventOutputId,
            lotId ?? LotId,
            quantity ?? Quantity,
            unitId ?? UnitId);
    }
}
