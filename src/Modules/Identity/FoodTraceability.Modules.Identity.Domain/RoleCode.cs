namespace FoodTraceability.Modules.Identity.Domain;

public sealed record RoleCode
{
    public const int MaximumLength = 64;

    private RoleCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RoleCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityDomainException(
                "Role code must not be null, empty, or consist only of whitespace.");
        }

        // Invariant casing keeps the same technical code identical in every server culture.
        var normalizedValue = value.Trim().ToUpperInvariant();

        if (normalizedValue.Length > MaximumLength)
        {
            throw new IdentityDomainException(
                $"Role code must not exceed {MaximumLength} characters.");
        }

        if (normalizedValue.Any(character =>
                character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_'))
        {
            throw new IdentityDomainException(
                "Role code may contain only letters A-Z, digits 0-9, and underscores.");
        }

        return new RoleCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
