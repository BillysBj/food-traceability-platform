namespace FoodTraceability.Modules.Catalog.Domain;

public sealed class Article
{
    public const int MaximumArticleNumberLength = 64;
    public const int MaximumGtinLength = 14;

    private Article(
        Guid id,
        Guid organizationId,
        Guid productId,
        string articleNumber,
        string? gtin,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        ProductId = productId;
        ArticleNumber = articleNumber;
        Gtin = gtin;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid ProductId { get; }

    public string ArticleNumber { get; }

    public string? Gtin { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Article Create(
        Guid id,
        Guid organizationId,
        Guid productId,
        string? articleNumber,
        string? gtin,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogDomainException("Article id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new CatalogDomainException("Organization id must not be empty.");
        }

        if (productId == Guid.Empty)
        {
            throw new CatalogDomainException("Product id must not be empty.");
        }

        return new Article(
            id,
            organizationId,
            productId,
            NormalizeArticleNumber(articleNumber),
            ValidateGtin(gtin),
            createdAt);
    }

    private static string NormalizeArticleNumber(string? articleNumber)
    {
        if (string.IsNullOrWhiteSpace(articleNumber))
        {
            throw new CatalogDomainException(
                "Article number must not be null, empty, or consist only of whitespace.");
        }

        var normalizedArticleNumber = articleNumber.Trim();
        if (normalizedArticleNumber.Length > MaximumArticleNumberLength)
        {
            throw new CatalogDomainException(
                $"Article number must not exceed {MaximumArticleNumberLength} characters.");
        }

        return normalizedArticleNumber;
    }

    private static string? ValidateGtin(string? gtin)
    {
        if (gtin is null)
        {
            return null;
        }

        if (!IsAllowedGtinLength(gtin.Length)
            || gtin.Any(character => character is < '0' or > '9'))
        {
            throw new CatalogDomainException(
                "GTIN must contain only digits and have a length of 8, 12, 13, or 14.");
        }

        return gtin;
    }

    private static bool IsAllowedGtinLength(int length)
    {
        return length is 8 or 12 or 13 or 14;
    }
}
