using Contracts = FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Traceability.Application.Events;

public sealed class TraceabilityEventCreator(CreateTraceabilityEventService service)
    : Contracts.ITraceabilityEventCreator
{
    public async Task<Contracts.CreateTraceabilityEventResult> CreateAsync(
        Contracts.CreateTraceabilityEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var created = await service.CreateAsync(
                new CreateTraceabilityEventCommand(
                    request.OrganizationId,
                    request.EventTypeCode,
                    request.LocationId,
                    request.OccurredAt,
                    request.ExternalReference,
                    request.Description,
                    request.CreatedBy,
                    MapLots(request.Inputs),
                    MapLots(request.Outputs)),
                cancellationToken);

            return new Contracts.CreateTraceabilityEventResult(created.Id, created.OccurredAt);
        }
        catch (TraceabilityEventValidationException exception)
        {
            throw new Contracts.TraceabilityEventValidationException(exception.Message);
        }
        catch (TraceabilityEventConflictException exception)
        {
            throw new Contracts.TraceabilityEventConflictException(exception.Message);
        }
    }

    private static TraceabilityEventLotCommand[]? MapLots(
        IReadOnlyList<Contracts.TraceabilityEventLot>? lots) =>
        lots?.Select(lot => lot is null
            // Preserve invalid entries for the existing service's validation.
            ? null!
            : new TraceabilityEventLotCommand(lot.LotId, lot.Quantity)).ToArray();
}
