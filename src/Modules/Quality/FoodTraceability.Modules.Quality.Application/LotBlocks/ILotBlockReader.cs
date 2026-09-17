namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public interface ILotBlockReader
{
    Task<LotBlockPage> ListAsync(ListLotBlocksQuery query, CancellationToken cancellationToken);
}
