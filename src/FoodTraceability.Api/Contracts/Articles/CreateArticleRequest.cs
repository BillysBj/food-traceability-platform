using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Api.Contracts.Articles;

public sealed record CreateArticleRequest(
    Guid ProductId,
    [Required, StringLength(Article.MaximumArticleNumberLength)] string? ArticleNumber,
    [RegularExpression(@"^(?:[0-9]{8}|[0-9]{12}|[0-9]{13}|[0-9]{14})$")]
    string? Gtin);
