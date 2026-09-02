using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Modules.Catalog.Application.Articles;

public interface IArticleReader
{
    Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken);

    Task<ArticleDetails?> FindByIdAsync(
        Guid organizationId,
        Guid articleId,
        CancellationToken cancellationToken);
}

public interface IArticleWriter
{
    Task AddAsync(Article article, CancellationToken cancellationToken);
}
