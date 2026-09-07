namespace FoodTraceability.Modules.Identity.Domain;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 256;

    // Deliberately not a value object: retaining plaintext password state would increase the
    // risk of disclosure through exceptions, logs, debugger dumps, or serialization.
    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new IdentityDomainException(
                "Password must not be null, empty, or consist only of whitespace.");
        }

        if (password.Length < MinimumLength)
        {
            throw new IdentityDomainException("Password does not meet the minimum length requirement.");
        }

        if (password.Length > MaximumLength)
        {
            throw new IdentityDomainException("Password exceeds the maximum length requirement.");
        }
    }
}
