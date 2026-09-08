using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Modules.Catalog.Application.Products;

public interface IProductReader
{
    Task<ProductDetails?> FindByIdAsync(
        Guid productId,
        CancellationToken cancellationToken);
}

public interface IProductWriter
{
    Task AddAsync(Product product, CancellationToken cancellationToken);
}
