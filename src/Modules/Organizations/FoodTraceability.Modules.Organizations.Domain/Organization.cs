namespace FoodTraceability.Modules.Organizations.Domain;

public sealed class Organization
{
    public const int MaximumNameLength = 200;
    public const int MaximumVatIdLength = 64;
    public const int MaximumTaxNumberLength = 64;
    public const int MaximumEmailLength = 254;
    public const int MaximumPhoneLength = 32;

    private Organization(
        Guid id,
        string name,
        string? vatId,
        string? taxNumber,
        string? email,
        string? phone,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        VatId = vatId;
        TaxNumber = taxNumber;
        Email = email;
        Phone = phone;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? VatId { get; }

    public string? TaxNumber { get; }

    public string? Email { get; }

    public string? Phone { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public static Organization Create(
        Guid id,
        string? name,
        string? vatId,
        string? taxNumber,
        string? email,
        string? phone,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new OrganizationsDomainException("Organization id must not be empty.");
        }

        return new Organization(
            id,
            NormalizeRequired(name, "Organization name", MaximumNameLength),
            NormalizeOptional(vatId, "VAT id", MaximumVatIdLength),
            NormalizeOptional(taxNumber, "Tax number", MaximumTaxNumberLength),
            NormalizeOptional(email, "Email", MaximumEmailLength),
            NormalizeOptional(phone, "Phone", MaximumPhoneLength),
            createdAt,
            createdAt);
    }

    private static string NormalizeRequired(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private static string? NormalizeOptional(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
