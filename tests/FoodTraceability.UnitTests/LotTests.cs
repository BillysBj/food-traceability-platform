using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class LotTests
{
    private static readonly Guid LotId =
        Guid.Parse("97411719-69af-40db-8fea-5399331a87ef");
    private static readonly Guid OrganizationId =
        Guid.Parse("f6238eb3-8c34-4d14-90d3-11131ac6cc44");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ValidLotIsCreatedWithTrimmedNumberAndOriginalCasing()
    {
        var lot = Lot.Create(LotId, OrganizationId, "  ABC-123  ", CreatedAt);

        Assert.Equal(LotId, lot.Id);
        Assert.Equal(OrganizationId, lot.OrganizationId);
        Assert.Equal("ABC-123", lot.LotNumber);
        Assert.Equal(CreatedAt, lot.CreatedAt);
    }

    [Fact]
    public void EmptyLotIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(
            () => Lot.Create(Guid.Empty, OrganizationId, "ABC-123", CreatedAt));
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(
            () => Lot.Create(LotId, Guid.Empty, "ABC-123", CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingLotNumberIsRejected(string? lotNumber)
    {
        Assert.Throws<TraceabilityDomainException>(
            () => Lot.Create(LotId, OrganizationId, lotNumber, CreatedAt));
    }

    [Fact]
    public void LotNumberAtMaximumLengthIsAccepted()
    {
        var lotNumber = new string('A', Lot.MaximumLotNumberLength);

        var lot = Lot.Create(LotId, OrganizationId, lotNumber, CreatedAt);

        Assert.Equal(lotNumber, lot.LotNumber);
    }

    [Fact]
    public void LotNumberOverMaximumLengthIsRejectedAfterTrimming()
    {
        var lotNumber = $"  {new string('A', Lot.MaximumLotNumberLength + 1)}  ";

        Assert.Throws<TraceabilityDomainException>(
            () => Lot.Create(LotId, OrganizationId, lotNumber, CreatedAt));
    }
}
