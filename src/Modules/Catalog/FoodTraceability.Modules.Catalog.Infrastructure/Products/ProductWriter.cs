using FoodTraceability.Modules.Catalog.Application.Products;
using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Products;

internal sealed class ProductWriter(CatalogDbContext dbContext) : IProductWriter
{
    private const string ProductCodeUniqueIndex = "ux_product_product_code_upper";

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        dbContext.Products.Add(product);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ProductCodeUniqueIndex
            })
        {
            throw new ProductConflictException(
                "A product with the same product code already exists.");
        }
    }
}
