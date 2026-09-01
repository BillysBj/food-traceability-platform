namespace FoodTraceability.Modules.Identity.Domain;

public sealed class User
{
    public const int MaximumNameLength = 100;

    private User(
        Guid id,
        EmailAddress email,
        string firstName,
        string lastName,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public EmailAddress Email { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(
        Guid id,
        EmailAddress? email,
        string? firstName,
        string? lastName,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("User id must not be empty.");
        }

        if (email is null)
        {
            throw new IdentityDomainException("User email address must be provided.");
        }

        return new User(
            id,
            email,
            NormalizeName(firstName, "First name"),
            NormalizeName(lastName, "Last name"),
            createdAt);
    }

    public void Deactivate(DateTimeOffset occurredAt)
    {
        EnsureOccurrenceIsNotBeforeCreation(occurredAt);

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = occurredAt;
    }

    public void Activate(DateTimeOffset occurredAt)
    {
        EnsureOccurrenceIsNotBeforeCreation(occurredAt);

        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = occurredAt;
    }

    private static string NormalizeName(string? name, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException(
                $"{fieldName} must not be null, empty, or consist only of whitespace.");
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
        {
            throw new IdentityDomainException(
                $"{fieldName} must not exceed {MaximumNameLength} characters.");
        }

        return normalizedName;
    }

    private void EnsureOccurrenceIsNotBeforeCreation(DateTimeOffset occurredAt)
    {
        if (occurredAt < CreatedAt)
        {
            throw new IdentityDomainException(
                "User state change must not occur before the user was created.");
        }
    }
}
