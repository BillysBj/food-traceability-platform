namespace FoodTraceability.Modules.Identity.Domain;

public sealed class UserCredential
{
    private UserCredential(
        Guid userId,
        string passwordHash,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid UserId { get; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserCredential Create(
        Guid userId,
        string? passwordHash,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("Credential user id must not be empty.");
        }

        var validPasswordHash = EnsurePasswordHashIsValid(passwordHash);
        EnsureUpdatedAtIsNotBeforeCreatedAt(createdAt, updatedAt);

        return new UserCredential(userId, validPasswordHash, createdAt, updatedAt);
    }

    public void ChangePasswordHash(string? passwordHash, DateTimeOffset updatedAt)
    {
        var validPasswordHash = EnsurePasswordHashIsValid(passwordHash);
        EnsureUpdatedAtIsNotBeforeCreatedAt(CreatedAt, updatedAt);

        PasswordHash = validPasswordHash;
        UpdatedAt = updatedAt;
    }

    private static string EnsurePasswordHashIsValid(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new IdentityDomainException(
                "Password hash must not be null, empty, or consist only of whitespace.");
        }

        return passwordHash;
    }

    private static void EnsureUpdatedAtIsNotBeforeCreatedAt(
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (updatedAt < createdAt)
        {
            throw new IdentityDomainException(
                "Credential update must not occur before the credential was created.");
        }
    }
}
