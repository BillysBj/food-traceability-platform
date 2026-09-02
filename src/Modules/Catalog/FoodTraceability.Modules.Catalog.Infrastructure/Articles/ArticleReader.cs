using FoodTraceability.Modules.Catalog.Application.Articles;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Articles;

internal sealed class ArticleReader(CatalogDbContext dbContext) : IArticleReader
{
    public Task<bool> ProductExistsAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken);
    }

    public Task<ArticleDetails?> FindByIdAsync(
        Guid organizationId,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        return dbContext.Articles
            .AsNoTracking()
            .Where(article => article.Id == articleId
                && article.OrganizationId == organizationId)
            .Select(article => new ArticleDetails(
                article.Id,
                article.OrganizationId,
                article.ProductId,
                article.ArticleNumber,
                article.Gtin,
                article.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
