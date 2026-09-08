namespace FoodTraceability.Modules.Catalog.Application.Products;

public sealed class ProductQueryService(IProductReader reader)
{
    public Task<ProductDetails?> FindByIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return productId == Guid.Empty
            ? Task.FromResult<ProductDetails?>(null)
            : reader.FindByIdAsync(productId, cancellationToken);
    }
}
