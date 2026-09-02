using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.UnitTests;

public sealed class ArticleTests
{
    private static readonly Guid ArticleId =
        Guid.Parse("7d829faa-b351-4ec2-b421-601b36c3299a");
    private static readonly Guid OrganizationId =
        Guid.Parse("17dfcd1f-d338-40dc-a197-8ce22f681675");
    private static readonly Guid ProductId =
        Guid.Parse("ead15ff3-5d51-4d31-8fc7-081fae946c3a");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidArticleIsCreatedWithTrimmedArticleNumberAndOriginalCasing()
    {
        var article = Article.Create(
            ArticleId,
            OrganizationId,
            ProductId,
            "  ART-1  ",
            "1234567890123",
            CreatedAt);

        Assert.Equal(ArticleId, article.Id);
        Assert.Equal(OrganizationId, article.OrganizationId);
        Assert.Equal(ProductId, article.ProductId);
        Assert.Equal("ART-1", article.ArticleNumber);
        Assert.Equal("1234567890123", article.Gtin);
        Assert.Equal(CreatedAt, article.CreatedAt);
    }

    [Fact]
    public void EmptyArticleIdIsRejected()
    {
        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(id: Guid.Empty));
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected()
    {
        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(organizationId: Guid.Empty));
    }

    [Fact]
    public void EmptyProductIdIsRejected()
    {
        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(productId: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingArticleNumberIsRejected(string? articleNumber)
    {
        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(articleNumber: articleNumber));
    }

    [Fact]
    public void ArticleNumberAtMaximumLengthIsAccepted()
    {
        var articleNumber = new string('A', Article.MaximumArticleNumberLength);

        var article = CreateArticle(articleNumber: articleNumber);

        Assert.Equal(articleNumber, article.ArticleNumber);
    }

    [Fact]
    public void ArticleNumberOverMaximumLengthIsRejectedAfterTrimming()
    {
        var articleNumber = $"  {new string('A', Article.MaximumArticleNumberLength + 1)}  ";

        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(articleNumber: articleNumber));
    }

    [Fact]
    public void NullGtinIsAccepted()
    {
        var article = CreateArticle(gtin: null);

        Assert.Null(article.Gtin);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("123456789012")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234")]
    public void AllowedGtinLengthsAreAccepted(string gtin)
    {
        var article = CreateArticle(gtin: gtin);

        Assert.Equal(gtin, article.Gtin);
    }

    [Theory]
    [InlineData("1234A678")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("123456789012345")]
    public void InvalidGtinFormatsAreRejected(string gtin)
    {
        Assert.Throws<CatalogDomainException>(
            () => CreateArticle(gtin: gtin));
    }

    private static Article CreateArticle(
        Guid? id = null,
        Guid? organizationId = null,
        Guid? productId = null,
        string? articleNumber = "ART-1",
        string? gtin = "12345678")
    {
        return Article.Create(
            id ?? ArticleId,
            organizationId ?? OrganizationId,
            productId ?? ProductId,
            articleNumber,
            gtin,
            CreatedAt);
    }
}
