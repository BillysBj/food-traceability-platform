using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Lots;

internal sealed class LotArticleReader(TraceabilityDbContext dbContext, ScopedTransaction transaction)
    : ILotArticleReader
{
    public async Task<Guid?> FindArticleIdAsync(
        Guid organizationId, Guid lotId, CancellationToken cancellationToken)
    {
        await transaction.EnlistAsync(dbContext, cancellationToken);
        return await dbContext.Lots.AsNoTracking()
            .Where(lot => lot.OrganizationId == organizationId && lot.Id == lotId)
            .Select(lot => (Guid?)lot.ArticleId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
