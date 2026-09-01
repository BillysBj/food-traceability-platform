namespace FoodTraceability.Modules.Identity.Domain;

public sealed record PermissionCode
{
    public const int MaximumLength = 128;

    private PermissionCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PermissionCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityDomainException(
                "Permission code must not be null, empty, or consist only of whitespace.");
        }

        // Invariant casing keeps the same technical code identical in every server culture.
        var normalizedValue = value.Trim().ToLowerInvariant();

        if (normalizedValue.Length > MaximumLength)
        {
            throw new IdentityDomainException(
                $"Permission code must not exceed {MaximumLength} characters.");
        }

        if (normalizedValue.Any(character =>
                character is not (>= 'a' and <= 'z')
                && character is not (>= '0' and <= '9')
                && character != '.'
                && character != '_'))
        {
            throw new IdentityDomainException(
                "Permission code may contain only letters a-z, digits 0-9, dots, and underscores.");
        }

        var segments = normalizedValue.Split('.');
        if (segments.Length < 2 || segments.Any(string.IsNullOrEmpty))
        {
            throw new IdentityDomainException(
                "Permission code must contain at least two non-empty dot-separated segments.");
        }

        return new PermissionCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
