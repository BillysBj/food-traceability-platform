namespace FoodTraceability.Modules.Catalog.Application.Articles;

public sealed class ArticleQueryService(IArticleReader reader)
{
    public Task<ArticleDetails?> FindByIdAsync(
        Guid organizationId,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        return organizationId == Guid.Empty || articleId == Guid.Empty
            ? Task.FromResult<ArticleDetails?>(null)
            : reader.FindByIdAsync(organizationId, articleId, cancellationToken);
    }
}
