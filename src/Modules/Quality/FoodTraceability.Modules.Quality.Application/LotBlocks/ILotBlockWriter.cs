using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public interface ILotBlockWriter
{
    /// <summary>Enlists in the active transaction and writes without committing it.</summary>
    /// <exception cref="LotBlockConflictException">The lot already has an open block.</exception>
    Task AddAsync(LotBlock block, CancellationToken cancellationToken);

    /// <summary>Enlists in the active transaction and reads exactly the tenant/lot/block tuple.</summary>
    Task<LotBlock?> FindAsync(Guid organizationId, Guid lotId, Guid blockId, CancellationToken cancellationToken);

    /// <summary>Writes only the release fields of a still-open block in the active transaction, without committing.</summary>
    /// <exception cref="LotBlockAlreadyReleasedException">Another release has already won.</exception>
    Task SaveReleaseAsync(LotBlock block, CancellationToken cancellationToken);
}
