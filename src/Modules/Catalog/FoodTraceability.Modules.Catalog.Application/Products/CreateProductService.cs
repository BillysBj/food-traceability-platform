using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Modules.Catalog.Application.Products;

public sealed class CreateProductService(
    IProductWriter writer,
    TimeProvider timeProvider)
{
    public async Task<ProductDetails> CreateAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Product product;
        try
        {
            product = Product.Create(
                Guid.NewGuid(),
                command.ProductCode,
                command.Name,
                timeProvider.GetUtcNow());
        }
        catch (CatalogDomainException exception)
        {
            throw new ProductValidationException(exception.Message);
        }

        await writer.AddAsync(product, cancellationToken);

        return new ProductDetails(
            product.Id,
            product.ProductCode,
            product.Name,
            product.CreatedAt);
    }
}
