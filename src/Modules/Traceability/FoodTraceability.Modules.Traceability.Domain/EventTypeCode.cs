namespace FoodTraceability.Modules.Traceability.Domain;

public sealed record EventTypeCode
{
    public const int MaximumLength = 32;

    private EventTypeCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EventTypeCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TraceabilityDomainException(
                "Event type code must not be null, empty, or consist only of whitespace.");
        }

        // Invariant casing keeps the same technical code identical in every server culture.
        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length > MaximumLength)
        {
            throw new TraceabilityDomainException(
                $"Event type code must not exceed {MaximumLength} characters.");
        }

        if (normalizedValue.Any(character =>
                character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_'))
        {
            throw new TraceabilityDomainException(
                "Event type code may contain only letters A-Z, digits 0-9, and underscores.");
        }

        return new EventTypeCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
