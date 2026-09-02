namespace FoodTraceability.Modules.Catalog.Domain;

public enum UnitDimension
{
    Mass,
    Volume,
    Count,
}

public static class UnitDimensionCodes
{
    public const string Mass = "MASS";
    public const string Volume = "VOLUME";
    public const string Count = "COUNT";
    public const int MaximumLength = 16;

    public static string ToCode(UnitDimension dimension)
    {
        return dimension switch
        {
            UnitDimension.Mass => Mass,
            UnitDimension.Volume => Volume,
            UnitDimension.Count => Count,
            _ => throw new CatalogDomainException("Unit dimension is invalid."),
        };
    }

    public static UnitDimension FromCode(string code)
    {
        return code switch
        {
            Mass => UnitDimension.Mass,
            Volume => UnitDimension.Volume,
            Count => UnitDimension.Count,
            _ => throw new CatalogDomainException("Unit dimension code is invalid."),
        };
    }
}
