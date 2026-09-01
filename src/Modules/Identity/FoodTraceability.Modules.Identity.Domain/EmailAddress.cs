namespace FoodTraceability.Modules.Identity.Domain;

public sealed record EmailAddress
{
    public const int MaximumLength = 254;

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityDomainException(
                "Email address must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim().ToLowerInvariant();

        if (normalizedValue.Length > MaximumLength)
        {
            throw new IdentityDomainException(
                $"Email address must not exceed {MaximumLength} characters.");
        }

        var separatorIndex = normalizedValue.IndexOf('@');
        if (separatorIndex < 0 || separatorIndex != normalizedValue.LastIndexOf('@'))
        {
            throw new IdentityDomainException("Email address must contain exactly one '@' character.");
        }

        if (separatorIndex == 0 || separatorIndex == normalizedValue.Length - 1)
        {
            throw new IdentityDomainException(
                "Email address must contain a non-empty local part and domain part.");
        }

        if (normalizedValue.Any(char.IsWhiteSpace))
        {
            throw new IdentityDomainException("Email address must not contain whitespace.");
        }

        return new EmailAddress(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }
}
