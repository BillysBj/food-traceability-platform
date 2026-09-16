using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainStatus = FoodTraceability.Modules.Traceability.Domain.LotQualityStatus;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Lots;

internal sealed class LotQualityStatusWriter(
    TraceabilityDbContext dbContext,
    ScopedTransaction scopedTransaction) : ILotQualityStatusWriter
{
    public async Task<bool> SetAsync(
        Guid organizationId,
        Guid lotId,
        LotQualityStatus qualityStatus,
        CancellationToken cancellationToken)
    {
        var status = qualityStatus switch
        {
            LotQualityStatus.Pending => DomainStatus.Pending,
            LotQualityStatus.Blocked => DomainStatus.Blocked,
            LotQualityStatus.Released => DomainStatus.Released,
            _ => throw new ArgumentOutOfRangeException(nameof(qualityStatus), qualityStatus,
                "Lot quality status is invalid."),
        };

        await using var transaction = scopedTransaction.IsActive
            ? null
            : await scopedTransaction.BeginAsync(cancellationToken);
        await scopedTransaction.EnlistAsync(dbContext, cancellationToken);

        // One tenant-filtered UPDATE touches only this column, including when an earlier
        // operation has already tracked the lot. No unrelated tracked changes are saved.
        var affected = await dbContext.Lots
            .Where(lot => lot.OrganizationId == organizationId && lot.Id == lotId)
            .ExecuteUpdateAsync(update => update.SetProperty(lot => lot.QualityStatus, status),
                cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return affected == 1;
    }
}
