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
