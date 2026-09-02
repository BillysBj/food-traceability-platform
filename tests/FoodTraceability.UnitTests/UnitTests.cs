using System.Globalization;
using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.UnitTests;

public sealed class UnitTests
{
    private static readonly Guid UnitId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnitCodeIsTrimmedAndUppercasedInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var unit = Unit.Create(
                UnitId,
                UnitCode.Create("  ki  "),
                "  kg  ",
                UnitDimension.Mass,
                CreatedAt);

            Assert.Equal("KI", unit.Code.Value);
            Assert.Equal("kg", unit.Symbol);
            Assert.Equal(UnitDimension.Mass, unit.Dimension);
            Assert.Equal(CreatedAt, unit.CreatedAt);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyUnitCodeIsRejected(string? code)
    {
        Assert.Throws<CatalogDomainException>(() => UnitCode.Create(code));
    }

    [Fact]
    public void UnitCodeOverMaximumLengthIsRejectedAfterTrimming()
    {
        var code = $"  {new string('A', UnitCode.MaximumLength + 1)}  ";

        Assert.Throws<CatalogDomainException>(() => UnitCode.Create(code));
    }

    [Fact]
    public void InvalidDimensionIsRejected()
    {
        Assert.Throws<CatalogDomainException>(() => Unit.Create(
            UnitId,
            UnitCode.Create("KG"),
            "kg",
            (UnitDimension)999,
            CreatedAt));
    }
}
