using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodTraceability.Platform.Persistence;

public static class SharedTransactionExtensions
{
    public static async Task<IDbContextTransaction> BeginSharedTransactionAsync(
        this DbContext owner,
        DbContext participant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(participant);

        if (!ReferenceEquals(owner.Database.GetDbConnection(), participant.Database.GetDbConnection()))
        {
            throw new InvalidOperationException(
                "A shared transaction requires both DbContexts to use the same DbConnection instance.");
        }

        var transaction = await owner.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await participant.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
