using System.Globalization;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class LotTests
{
    private static readonly Guid LotId =
        Guid.Parse("97411719-69af-40db-8fea-5399331a87ef");
    private static readonly Guid OrganizationId =
        Guid.Parse("f6238eb3-8c34-4d14-90d3-11131ac6cc44");
    private static readonly Guid ArticleId =
        Guid.Parse("2f0f2b41-6b4a-4a0e-9a4a-1f0d3c5e7b21");
    private static readonly Guid UnitId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 9, 30, 0, TimeSpan.Zero);
    private const decimal Quantity = 1250.5m;

    [Fact]
    public void ValidLotIsCreatedWithTrimmedNumberAndOriginalCasing()
    {
        var lot = Create(lotNumber: "  ABC-123  ");

        Assert.Equal(LotId, lot.Id);
        Assert.Equal(OrganizationId, lot.OrganizationId);
        Assert.Equal(ArticleId, lot.ArticleId);
        Assert.Equal("ABC-123", lot.LotNumber);
        Assert.Equal(Quantity, lot.Quantity);
        Assert.Equal(UnitId, lot.UnitId);
        Assert.Equal(CreatedAt, lot.CreatedAt);
    }

    [Fact]
    public void EmptyLotIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => Create(id: Guid.Empty));
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => Create(organizationId: Guid.Empty));
    }

    [Fact]
    public void EmptyArticleIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => Create(articleId: Guid.Empty));
    }

    [Fact]
    public void EmptyUnitIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => Create(unitId: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingLotNumberIsRejected(string? lotNumber)
    {
        Assert.Throws<TraceabilityDomainException>(() => Create(lotNumber: lotNumber));
    }

    [Fact]
    public void LotNumberAtMaximumLengthIsAccepted()
    {
        var lotNumber = new string('A', Lot.MaximumLotNumberLength);

        var lot = Create(lotNumber: lotNumber);

        Assert.Equal(lotNumber, lot.LotNumber);
    }

    [Fact]
    public void LotNumberOverMaximumLengthIsRejectedAfterTrimming()
    {
        var lotNumber = $"  {new string('A', Lot.MaximumLotNumberLength + 1)}  ";

        Assert.Throws<TraceabilityDomainException>(() => Create(lotNumber: lotNumber));
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
        var lot = Create(quantity: 0.000001m);

        Assert.Equal(0.000001m, lot.Quantity);
    }

    private static Lot Create(
        Guid? id = null,
        Guid? organizationId = null,
        Guid? articleId = null,
        string? lotNumber = "ABC-123",
        decimal? quantity = null,
        Guid? unitId = null)
    {
        return Lot.Create(
            id ?? LotId,
            organizationId ?? OrganizationId,
            articleId ?? ArticleId,
            lotNumber,
            quantity ?? Quantity,
            unitId ?? UnitId,
            CreatedAt);
    }
}
