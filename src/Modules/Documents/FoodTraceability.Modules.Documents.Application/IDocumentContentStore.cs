using FoodTraceability.Modules.Documents.Domain;

namespace FoodTraceability.Modules.Documents.Application;

public interface IDocumentContentStore
{
    Task AddAsync(DocumentContent content, CancellationToken cancellationToken);

    /// <summary>Returns a stream owned by the caller, or null if the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}
