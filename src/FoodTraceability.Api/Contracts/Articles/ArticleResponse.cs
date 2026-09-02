namespace FoodTraceability.Api.Contracts.Articles;

public sealed record ArticleResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ProductId,
    string ArticleNumber,
    string? Gtin,
    DateTimeOffset CreatedAt);
