using FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed class LotBlockQueryService(ILotBlockReader reader, ILotArticleReader lotReader)
{
    public async Task<LotBlockPage?> ListAsync(ListLotBlocksQuery query, CancellationToken cancellationToken)
    {
        if (await lotReader.FindArticleIdAsync(query.OrganizationId, query.LotId, cancellationToken) is null)
        {
            return null;
        }

        return await reader.ListAsync(query, cancellationToken);
    }
}
