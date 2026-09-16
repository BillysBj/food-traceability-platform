using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public interface ILotBlockWriter
{
    /// <summary>Enlists in the active transaction and writes without committing it.</summary>
    /// <exception cref="LotBlockConflictException">The lot already has an open block.</exception>
    Task AddAsync(LotBlock block, CancellationToken cancellationToken);
}
