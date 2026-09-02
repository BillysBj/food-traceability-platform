namespace FoodTraceability.Modules.Catalog.Domain;

public sealed class Unit
{
    public const int MaximumSymbolLength = 16;

    private Unit(
        Guid id,
        UnitCode code,
        string symbol,
        UnitDimension dimension,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        Symbol = symbol;
        Dimension = dimension;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public UnitCode Code { get; }

    /// <summary>
    /// Technical notation for the unit, not a localized display name. SI symbols are
    /// international (kg, g, l, ml); pcs is a neutral placeholder, not display text.
    /// </summary>
    public string Symbol { get; }

    public UnitDimension Dimension { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Unit Create(
        Guid id,
        UnitCode? code,
        string? symbol,
        UnitDimension dimension,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new CatalogDomainException("Unit id must not be empty.");
        }

        if (code is null)
        {
            throw new CatalogDomainException("Unit code must be provided.");
        }

        if (dimension is not UnitDimension.Mass
            and not UnitDimension.Volume
            and not UnitDimension.Count)
        {
            throw new CatalogDomainException("Unit dimension must be valid.");
        }

        return new Unit(id, code, NormalizeSymbol(symbol), dimension, createdAt);
    }

    private static string NormalizeSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new CatalogDomainException(
                "Unit symbol must not be null, empty, or consist only of whitespace.");
        }

        var normalizedSymbol = symbol.Trim();
        if (normalizedSymbol.Length > MaximumSymbolLength)
        {
            throw new CatalogDomainException(
                $"Unit symbol must not exceed {MaximumSymbolLength} characters.");
        }

        return normalizedSymbol;
    }
}
