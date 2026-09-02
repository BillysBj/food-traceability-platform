namespace FoodTraceability.Modules.Catalog.Domain;

public sealed class Product
{
    public const int MaximumProductCodeLength = 64;
    public const int MaximumNameLength = 200;

    private Product(
        Guid id,
        string productCode,
        string name,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProductCode = productCode;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string ProductCode { get; }

    /// <summary>
    /// The primary business designation entered by the platform operator. The language
    /// of this value is deliberately NOT modelled. This is NOT a localized UI string and
    /// NOT an i18n solution. Whether this value later becomes the English or general
    /// fallback of a translation structure is EXPLICITLY NOT YET DECIDED and remains
    /// reserved for the open i18n decision D-07.
    /// </summary>
    public string Name { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Product Create(
        Guid id,
        string? productCode,
        string? name,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogDomainException("Product id must not be empty.");
        }

        return new Product(
            id,
            NormalizeRequired(
                productCode,
                "Product code",
                MaximumProductCodeLength),
            NormalizeRequired(name, "Product name", MaximumNameLength),
            createdAt);
    }

    private static string NormalizeRequired(
        string? value,
        string fieldName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CatalogDomainException(
                $"{fieldName} must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new CatalogDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
