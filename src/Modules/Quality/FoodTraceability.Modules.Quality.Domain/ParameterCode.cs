namespace FoodTraceability.Modules.Quality.Domain;

public sealed record ParameterCode
{
    // Allows descriptive technical identifiers while keeping indexed codes bounded.
    public const int MaximumLength = 64;

    private ParameterCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ParameterCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new QualityDomainException(
                "Parameter code must not be null, empty, or consist only of whitespace.");
        }

        // Invariant casing keeps the same technical code identical in every server culture.
        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length > MaximumLength)
        {
            throw new QualityDomainException(
                $"Parameter code must not exceed {MaximumLength} characters.");
        }

        if (normalizedValue.Any(character =>
                character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_'))
        {
            throw new QualityDomainException(
                "Parameter code may contain only letters A-Z, digits 0-9, and underscores.");
        }

        return new ParameterCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
