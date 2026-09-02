namespace FoodTraceability.Modules.Catalog.Domain;

public sealed class CatalogDomainException : Exception
{
    public CatalogDomainException(string message)
        : base(message)
    {
    }
}
