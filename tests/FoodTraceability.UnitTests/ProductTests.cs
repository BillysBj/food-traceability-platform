using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.UnitTests;

public sealed class ProductTests
{
    private static readonly Guid ProductId =
        Guid.Parse("92481ad8-947a-4545-a0ac-fb85623b4830");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidProductIsCreatedWithTrimmedValuesAndOriginalProductCodeCasing()
    {
        var product = Product.Create(
            ProductId,
            "  OLIVE-OIL-EV  ",
            "  Extra Virgin Olive Oil  ",
            CreatedAt);

        Assert.Equal(ProductId, product.Id);
        Assert.Equal("OLIVE-OIL-EV", product.ProductCode);
        Assert.Equal("Extra Virgin Olive Oil", product.Name);
        Assert.Equal(CreatedAt, product.CreatedAt);
    }

    [Fact]
    public void EmptyProductIdIsRejected()
    {
        Assert.Throws<CatalogDomainException>(
            () => Product.Create(Guid.Empty, "OLIVE-OIL-EV", "Olive Oil", CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingProductCodeIsRejected(string? productCode)
    {
        Assert.Throws<CatalogDomainException>(
            () => Product.Create(ProductId, productCode, "Olive Oil", CreatedAt));
    }

    [Fact]
    public void ProductCodeAtMaximumLengthIsAccepted()
    {
        var productCode = new string('A', Product.MaximumProductCodeLength);

        var product = Product.Create(ProductId, productCode, "Olive Oil", CreatedAt);

        Assert.Equal(productCode, product.ProductCode);
    }

    [Fact]
    public void ProductCodeOverMaximumLengthIsRejectedAfterTrimming()
    {
        var productCode = $"  {new string('A', Product.MaximumProductCodeLength + 1)}  ";

        Assert.Throws<CatalogDomainException>(
            () => Product.Create(ProductId, productCode, "Olive Oil", CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string? name)
    {
        Assert.Throws<CatalogDomainException>(
            () => Product.Create(ProductId, "OLIVE-OIL-EV", name, CreatedAt));
    }

    [Fact]
    public void NameAtMaximumLengthIsAccepted()
    {
        var name = new string('A', Product.MaximumNameLength);

        var product = Product.Create(ProductId, "OLIVE-OIL-EV", name, CreatedAt);

        Assert.Equal(name, product.Name);
    }

    [Fact]
    public void NameOverMaximumLengthIsRejectedAfterTrimming()
    {
        var name = $"  {new string('A', Product.MaximumNameLength + 1)}  ";

        Assert.Throws<CatalogDomainException>(
            () => Product.Create(ProductId, "OLIVE-OIL-EV", name, CreatedAt));
    }
}
