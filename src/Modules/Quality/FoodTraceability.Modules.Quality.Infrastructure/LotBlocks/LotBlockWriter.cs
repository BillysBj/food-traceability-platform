using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Quality.Infrastructure.LotBlocks;

internal sealed class LotBlockWriter(QualityDbContext dbContext, ScopedTransaction transaction)
    : ILotBlockWriter
{
    private const string OpenBlockUniqueIndex = "ux_lot_block_lot_id_open";

    public async Task<LotBlock?> FindAsync(
        Guid organizationId, Guid lotId, Guid blockId, CancellationToken cancellationToken)
    {
        RequireActiveTransaction();
        await transaction.EnlistAsync(dbContext, cancellationToken);
        return await dbContext.LotBlocks.AsNoTracking().SingleOrDefaultAsync(
            block => block.OrganizationId == organizationId && block.LotId == lotId && block.Id == blockId,
            cancellationToken);
    }

    public async Task SaveReleaseAsync(LotBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);
        RequireActiveTransaction();
        if (block.ReleasedAt is null || block.ReleasedBy is null)
        {
            throw new InvalidOperationException("The block must be released before saving its release.");
        }

        await transaction.EnlistAsync(dbContext, cancellationToken);
        // Preserve the lot-before-block write order used by BlockLotService. Only the
        // winner can change the release pair; no stale tracked entity can overwrite it.
        var affected = await dbContext.LotBlocks
            .Where(current => current.OrganizationId == block.OrganizationId
                && current.LotId == block.LotId && current.Id == block.Id && current.ReleasedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(current => current.ReleasedAt, block.ReleasedAt)
                .SetProperty(current => current.ReleasedBy, block.ReleasedBy), cancellationToken);
        if (affected != 1)
        {
            throw new LotBlockAlreadyReleasedException();
        }
    }

    private void RequireActiveTransaction()
    {
        if (!transaction.IsActive)
        {
            throw new InvalidOperationException("Lot block release requires an active application transaction.");
        }
    }

    public async Task AddAsync(LotBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);

        await transaction.EnlistAsync(dbContext, cancellationToken);
        dbContext.LotBlocks.Add(block);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: OpenBlockUniqueIndex,
            })
        {
            throw new LotBlockConflictException("The lot already has an open block.");
        }
    }
}
