using FoodTraceability.Modules.Quality.Application.LotBlocks;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.LotBlocks;

internal sealed class LotBlockReader(QualityDbContext dbContext) : ILotBlockReader
{
    public async Task<LotBlockPage> ListAsync(ListLotBlocksQuery query, CancellationToken cancellationToken)
    {
        var blocks = dbContext.LotBlocks.AsNoTracking()
            .Where(block => block.OrganizationId == query.OrganizationId && block.LotId == query.LotId);
        var totalCount = await blocks.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new LotBlockPage([], query.Page, query.PageSize, totalCount);
        }

        var items = await blocks
            .OrderByDescending(block => block.BlockedAt)
            .ThenByDescending(block => block.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(block => new LotBlockListItem(
                block.Id, block.LotId, block.Reason, block.BlockedAt, block.BlockedBy,
                block.ReleasedAt, block.ReleasedBy))
            .ToListAsync(cancellationToken);
        return new LotBlockPage(items, query.Page, query.PageSize, totalCount);
    }
}
