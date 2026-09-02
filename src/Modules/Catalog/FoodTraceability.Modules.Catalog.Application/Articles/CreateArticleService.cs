using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Modules.Catalog.Application.Articles;

public sealed class CreateArticleService(
    IArticleReader reader,
    IArticleWriter writer,
    TimeProvider timeProvider)
{
    public async Task<ArticleDetails> CreateAsync(
        CreateArticleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ProductId == Guid.Empty
            || !await reader.ProductExistsAsync(command.ProductId, cancellationToken))
        {
            throw new ArticleValidationException(
                "The referenced product does not exist.");
        }

        Article article;
        try
        {
            article = Article.Create(
                Guid.NewGuid(),
                command.OrganizationId,
                command.ProductId,
                command.ArticleNumber,
                command.Gtin,
                timeProvider.GetUtcNow());
        }
        catch (CatalogDomainException exception)
        {
            throw new ArticleValidationException(exception.Message);
        }

        await writer.AddAsync(article, cancellationToken);

        return new ArticleDetails(
            article.Id,
            article.OrganizationId,
            article.ProductId,
            article.ArticleNumber,
            article.Gtin,
            article.CreatedAt);
    }
}
