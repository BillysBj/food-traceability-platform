namespace FoodTraceability.Modules.Organizations.Domain;

public sealed record CountryCode
{
    public const int Length = 2;

    private CountryCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CountryCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrganizationsDomainException(
                "Country code must not be null, empty, or consist only of whitespace.");
        }

        // Invariant casing keeps the same technical code identical in every server culture.
        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length != Length
            || normalizedValue.Any(character => character is not (>= 'A' and <= 'Z')))
        {
            throw new OrganizationsDomainException(
                $"Country code must consist of exactly {Length} letters from A to Z.");
        }

        return new CountryCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
