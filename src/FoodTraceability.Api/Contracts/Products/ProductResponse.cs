namespace FoodTraceability.Api.Contracts.Products;

public sealed record ProductResponse(
    Guid Id,
    string ProductCode,
    string Name,
    DateTimeOffset CreatedAt);
