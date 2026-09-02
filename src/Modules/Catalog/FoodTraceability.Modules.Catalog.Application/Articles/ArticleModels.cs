namespace FoodTraceability.Modules.Catalog.Application.Articles;

public sealed record CreateArticleCommand(
    Guid OrganizationId,
    Guid ProductId,
    string? ArticleNumber,
    string? Gtin);

public sealed record ArticleDetails(
    Guid Id,
    Guid OrganizationId,
    Guid ProductId,
    string ArticleNumber,
    string? Gtin,
    DateTimeOffset CreatedAt);

public enum ArticleConflictField
{
    ArticleNumber,
    Gtin
}
