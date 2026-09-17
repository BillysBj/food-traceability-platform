namespace FoodTraceability.Modules.Documents.Domain;

public sealed class DocumentContent
{
    private DocumentContent(string storageKey, byte[] content)
    {
        StorageKey = storageKey;
        Content = (byte[])content.Clone();
    }

    public string StorageKey { get; }
    public byte[] Content { get; }

    public static DocumentContent Create(string? storageKey, byte[]? content)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > Document.MaximumStorageKeyLength)
        {
            throw new DocumentsDomainException(
                $"Document storage key must not be empty and must not exceed {Document.MaximumStorageKeyLength} characters.");
        }

        if (content is null || content.Length == 0)
        {
            throw new DocumentsDomainException("Document content must not be null or empty.");
        }

        return new DocumentContent(storageKey, content);
    }
}
