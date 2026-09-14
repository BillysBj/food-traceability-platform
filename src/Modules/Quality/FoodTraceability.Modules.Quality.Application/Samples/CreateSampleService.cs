using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.Modules.Quality.Application.Samples;

public sealed class CreateSampleService(
    IApplicationTransaction transaction,
    ITraceabilityEventCreator eventCreator,
    ISampleWriter writer,
    TimeProvider timeProvider)
{
    private const string SampleEventTypeCode = "SAMPLE";

    public async Task<SampleDetails> CreateAsync(
        CreateSampleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var takenAt = TimestampPrecision.TruncateToMicroseconds(command.TakenAt);
        await using var handle = await transaction.BeginAsync(cancellationToken);
        try
        {
            var traceabilityEvent = await eventCreator.CreateAsync(
                new CreateTraceabilityEventRequest(
                    command.OrganizationId,
                    SampleEventTypeCode,
                    command.LocationId,
                    takenAt,
                    ExternalReference: null,
                    Description: null,
                    command.CreatedBy,
                    [new TraceabilityEventLot(command.LotId, command.Quantity)],
                    []),
                cancellationToken);

            var sample = Sample.Create(
                Guid.NewGuid(),
                command.OrganizationId,
                command.LotId,
                command.LocationId,
                traceabilityEvent.EventId,
                command.SampleNumber,
                takenAt,
                timeProvider.GetUtcNow());
            await writer.AddAsync(sample, cancellationToken);
            await handle.CommitAsync(cancellationToken);

            return new SampleDetails(
                sample.Id,
                sample.SampleNumber,
                sample.Status,
                sample.TakenAt,
                sample.LotId,
                sample.LocationId,
                sample.TraceabilityEventId);
        }
        catch (TraceabilityEventValidationException exception)
        {
            throw new SampleValidationException(exception.Message);
        }
        catch (TraceabilityEventConflictException exception)
        {
            throw new SampleConflictException(exception.Message);
        }
        catch (QualityDomainException exception)
        {
            throw new SampleValidationException(exception.Message);
        }
    }
}
