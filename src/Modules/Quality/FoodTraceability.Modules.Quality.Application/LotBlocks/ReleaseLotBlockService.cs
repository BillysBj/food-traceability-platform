using FoodTraceability.BuildingBlocks;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed class ReleaseLotBlockService(
    IApplicationTransaction transaction,
    ILotQualityStatusWriter statusWriter,
    ILotBlockWriter writer,
    TimeProvider timeProvider)
{
    public async Task<ReleasedLotBlockDetails> ReleaseAsync(
        ReleaseLotBlockCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var handle = await transaction.BeginAsync(cancellationToken);
        var block = await writer.FindAsync(command.OrganizationId, command.LotId, command.BlockId,
            cancellationToken) ?? throw new ReleaseLotBlockNotFoundException();
        if (block.ReleasedAt is not null)
        {
            throw new LotBlockAlreadyReleasedException();
        }

        var now = TimestampPrecision.TruncateToMicroseconds(timeProvider.GetUtcNow());
        block.Release(now, command.ReleasedBy);
        if (!await statusWriter.SetAsync(command.OrganizationId, command.LotId,
                LotQualityStatus.Released, cancellationToken))
        {
            throw new ReleaseLotBlockNotFoundException();
        }

        // A conditional write arbitrates concurrent releases. On conflict or failure,
        // disposing the uncommitted handle also rolls back the preceding lot status write.
        await writer.SaveReleaseAsync(block, cancellationToken);
        await handle.CommitAsync(cancellationToken);

        return new ReleasedLotBlockDetails(block.Id, block.LotId, block.Reason,
            block.BlockedAt, block.BlockedBy, now, command.ReleasedBy, LotQualityStatus.Released);
    }
}
