using FoodTraceability.Modules.Documents.Application;
using FoodTraceability.Modules.Documents.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Documents.Infrastructure;

internal sealed class DocumentContentStore(DocumentsDbContext dbContext, ScopedTransaction transaction)
    : IDocumentContentStore
{
    public async Task AddAsync(DocumentContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        await transaction.EnlistAsync(dbContext, cancellationToken);
        dbContext.DocumentContents.Add(content);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var content = await dbContext.DocumentContents.AsNoTracking()
            .Where(row => row.StorageKey == storageKey)
            .Select(row => row.Content)
            .SingleOrDefaultAsync(cancellationToken);

        return content is null ? null : new MemoryStream(content, writable: false);
    }
}
