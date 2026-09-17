namespace FoodTraceability.Modules.Documents.Domain;

public sealed record DocumentTypeCode
{
    public const int MaximumLength = 64;

    private DocumentTypeCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static DocumentTypeCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentsDomainException(
                "Document type code must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim().ToUpperInvariant();
        if (normalizedValue.Length > MaximumLength)
        {
            throw new DocumentsDomainException(
                $"Document type code must not exceed {MaximumLength} characters.");
        }

        if (normalizedValue.Any(character =>
                character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_'))
        {
            throw new DocumentsDomainException(
                "Document type code may contain only letters A-Z, digits 0-9, and underscores.");
        }

        return new DocumentTypeCode(normalizedValue);
    }

    public override string ToString() => Value;
}
