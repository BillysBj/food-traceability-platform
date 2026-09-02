using FoodTraceability.Modules.Catalog.Application.Articles;
using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateArticleServiceTests
{
    [Fact]
    public async Task CreateUsesRouteOrganizationAndPersistsArticle()
    {
        var organizationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var store = new StubArticleStore(productExists: true);
        var service = new CreateArticleService(store, store, new FixedTimeProvider(now));

        var result = await service.CreateAsync(
            new CreateArticleCommand(
                organizationId,
                productId,
                "  SKU-001  ",
                "12345678"),
            CancellationToken.None);

        var persisted = Assert.IsType<Article>(store.Article);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(organizationId, persisted.OrganizationId);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal("SKU-001", result.ArticleNumber);
        Assert.Equal("12345678", result.Gtin);
        Assert.Equal(now, result.CreatedAt);
    }

    [Fact]
    public async Task UnknownProductIsValidationErrorAndNothingIsPersisted()
    {
        var store = new StubArticleStore(productExists: false);
        var service = new CreateArticleService(store, store, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArticleValidationException>(() =>
            service.CreateAsync(
                new CreateArticleCommand(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "SKU-001",
                    null),
                CancellationToken.None));

        Assert.Equal("The referenced product does not exist.", exception.Message);
        Assert.Null(store.Article);
    }

    [Fact]
    public async Task InvalidGtinIsValidationErrorAndNothingIsPersisted()
    {
        var store = new StubArticleStore(productExists: true);
        var service = new CreateArticleService(store, store, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArticleValidationException>(() =>
            service.CreateAsync(
                new CreateArticleCommand(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "SKU-001",
                    "1234A678"),
                CancellationToken.None));

        Assert.Contains("GTIN", exception.Message, StringComparison.Ordinal);
        Assert.Null(store.Article);
    }

    private sealed class StubArticleStore(bool productExists) : IArticleReader, IArticleWriter
    {
        public Article? Article { get; private set; }

        public Task AddAsync(Article article, CancellationToken cancellationToken)
        {
            Article = article;
            return Task.CompletedTask;
        }

        public Task<ArticleDetails?> FindByIdAsync(
            Guid organizationId,
            Guid articleId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ArticleDetails?>(null);
        }

        public Task<bool> ProductExistsAsync(
            Guid productId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(productExists);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
