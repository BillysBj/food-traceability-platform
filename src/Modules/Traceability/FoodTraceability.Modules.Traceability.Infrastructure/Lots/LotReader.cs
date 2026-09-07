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

    public async Task<LotPage> ListAsync(
        ListLotsQuery query,
        CancellationToken cancellationToken)
    {
        var lots = dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.OrganizationId == query.OrganizationId);

        if (query.ArticleId is Guid articleId)
        {
            lots = lots.Where(lot => lot.ArticleId == articleId);
        }

        if (query.LotNumber is not null)
        {
            var normalizedLotNumber = query.LotNumber.ToUpperInvariant();
            lots = lots.Where(lot => lot.LotNumber.ToUpper() == normalizedLotNumber);
        }

        var totalCount = await lots.LongCountAsync(cancellationToken);
        var items = await lots
            .OrderByDescending(lot => lot.CreatedAt)
            .ThenByDescending(lot => lot.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(lot => new LotDetails(
                lot.Id,
                lot.OrganizationId,
                lot.ArticleId,
                lot.LotNumber,
                lot.Quantity,
                lot.UnitId,
                lot.CreatedAt))
            .ToListAsync(cancellationToken);

        return new LotPage(items, query.Page, query.PageSize, totalCount);
    }
}
