namespace FoodTraceability.Modules.Catalog.Application.Products;

public sealed class ProductValidationException : Exception
{
    public ProductValidationException(string message)
        : base(message)
    {
    }
}

public sealed class ProductConflictException : Exception
{
    public ProductConflictException(string message)
        : base(message)
    {
    }
}
