namespace FoodTraceability.Modules.Catalog.Application.Products;

public sealed record CreateProductCommand(string? ProductCode, string? Name);

public sealed record ProductDetails(
    Guid Id,
    string ProductCode,
    string Name,
    DateTimeOffset CreatedAt);
