using FoodTraceability.Modules.Traceability.Application.Lots;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Lots;

internal sealed class LotReader(TraceabilityDbContext dbContext) : ILotReader
{
    public Task<LotDetails?> FindByIdAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        return dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.Id == lotId
                && lot.OrganizationId == organizationId)
            .Select(lot => new LotDetails(
                lot.Id,
                lot.OrganizationId,
                lot.ArticleId,
                lot.LotNumber,
                lot.Quantity,
                lot.UnitId,
                lot.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
