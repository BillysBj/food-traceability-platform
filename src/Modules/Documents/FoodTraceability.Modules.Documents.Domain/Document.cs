namespace FoodTraceability.Modules.Documents.Domain;

public sealed class Document
{
    public const int MaximumNameLength = 200;
    public const int MaximumFileNameLength = 255;
    public const int MaximumStorageKeyLength = 1024;
    public const int MaximumMimeTypeLength = 255;
    public const int Sha256Length = 64;

    private Document(
        Guid id,
        Guid documentTypeId,
        Guid organizationId,
        string name,
        string fileName,
        string storageKey,
        string mimeType,
        DateOnly documentDate,
        string sha256,
        DateTimeOffset createdAt)
    {
        Id = id;
        DocumentTypeId = documentTypeId;
        OrganizationId = organizationId;
        Name = name;
        FileName = fileName;
        StorageKey = storageKey;
        MimeType = mimeType;
        DocumentDate = documentDate;
        Sha256 = sha256;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid DocumentTypeId { get; }
    public Guid OrganizationId { get; }
    public string Name { get; }
    public string FileName { get; }
    public string StorageKey { get; }
    public string MimeType { get; }
    public DateOnly DocumentDate { get; }
    public string Sha256 { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Document Create(
        Guid id,
        Guid documentTypeId,
        Guid organizationId,
        string? name,
        string? fileName,
        string? storageKey,
        string? mimeType,
        DateOnly documentDate,
        string? sha256,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DocumentsDomainException("Document id must not be empty.");
        }

        if (documentTypeId == Guid.Empty)
        {
            throw new DocumentsDomainException("Document type id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new DocumentsDomainException("Document organization id must not be empty.");
        }

        var normalizedName = RequiredTrimmedValue(name, MaximumNameLength, "Document name");
        var normalizedFileName = RequiredTrimmedValue(fileName, MaximumFileNameLength, "Document file name");
        if (normalizedFileName.Contains('/') || normalizedFileName.Contains('\\'))
        {
            throw new DocumentsDomainException("Document file name must not contain path separators.");
        }

        // Preserve the opaque key; DOC-002 defines its format.
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > MaximumStorageKeyLength)
        {
            throw new DocumentsDomainException(
                $"Document storage key must not be empty and must not exceed {MaximumStorageKeyLength} characters.");
        }

        var normalizedMimeType = RequiredTrimmedValue(mimeType, MaximumMimeTypeLength, "Document MIME type")
            .ToLowerInvariant();
        var separator = normalizedMimeType.IndexOf('/');
        if (separator <= 0 || separator == normalizedMimeType.Length - 1
            || normalizedMimeType.LastIndexOf('/') != separator
            || string.IsNullOrWhiteSpace(normalizedMimeType[..separator])
            || string.IsNullOrWhiteSpace(normalizedMimeType[(separator + 1)..]))
        {
            throw new DocumentsDomainException("Document MIME type must contain exactly one slash with a type and subtype.");
        }

        if (sha256 is null || sha256.Length != Sha256Length
            || sha256.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f') and not (>= 'A' and <= 'F')))
        {
            throw new DocumentsDomainException($"Document SHA-256 must contain exactly {Sha256Length} hexadecimal characters.");
        }

        // D-36: the Application caller normalizes timestamps; the aggregate preserves them.
        return new Document(id, documentTypeId, organizationId, normalizedName, normalizedFileName,
            storageKey, normalizedMimeType, documentDate, sha256.ToLowerInvariant(), createdAt);
    }

    private static string RequiredTrimmedValue(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentsDomainException($"{field} must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new DocumentsDomainException($"{field} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
