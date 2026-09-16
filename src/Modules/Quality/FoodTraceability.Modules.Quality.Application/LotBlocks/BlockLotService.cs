using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed class BlockLotService(
    IApplicationTransaction transaction,
    ILotQualityStatusWriter statusWriter,
    ILotBlockWriter writer,
    TimeProvider timeProvider)
{
    public async Task<LotBlockDetails> BlockAsync(
        BlockLotCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var handle = await transaction.BeginAsync(cancellationToken);
        try
        {
            var now = TimestampPrecision.TruncateToMicroseconds(timeProvider.GetUtcNow());
            var block = LotBlock.Create(Guid.NewGuid(), command.OrganizationId, command.LotId,
                command.Reason, now, command.BlockedBy, now);

            if (!await statusWriter.SetAsync(command.OrganizationId, command.LotId,
                    LotQualityStatus.Blocked, cancellationToken))
            {
                throw new LotBlockNotFoundException();
            }

            // The unique open-block index decides conflicts after the status write.
            // Disposing the uncommitted handle rolls back both modules on any failure.
            await writer.AddAsync(block, cancellationToken);
            await handle.CommitAsync(cancellationToken);

            return new LotBlockDetails(block.Id, block.LotId, block.Reason,
                block.BlockedAt, block.BlockedBy, LotQualityStatus.Blocked);
        }
        catch (QualityDomainException exception)
        {
            throw new LotBlockValidationException(exception.Message);
        }
    }
}
