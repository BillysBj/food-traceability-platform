using FoodTraceability.Modules.Catalog.Application.Products;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Products;

internal sealed class ProductReader(CatalogDbContext dbContext) : IProductReader
{
    public Task<ProductDetails?> FindByIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new ProductDetails(
                product.Id,
                product.ProductCode,
                product.Name,
                product.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
