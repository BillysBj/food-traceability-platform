using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.Events;

public sealed class CreateTraceabilityEventService(
    ITraceabilityEventWriter writer,
    TimeProvider timeProvider)
{
    private const decimal MaximumSupportedQuantity = 999999999999.999999m;

    public async Task<TraceabilityEventDetails> CreateAsync(
        CreateTraceabilityEventCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Inputs is null)
        {
            throw new TraceabilityEventValidationException(
                "Traceability event inputs must not be null.");
        }

        if (command.Outputs is null)
        {
            throw new TraceabilityEventValidationException(
                "Traceability event outputs must not be null.");
        }

        if (command.Inputs.Count == 0 && command.Outputs.Count == 0)
        {
            throw new TraceabilityEventValidationException(
                "Traceability event must have at least one input or output.");
        }

        ValidateQuantities(command.Inputs, "input");
        ValidateQuantities(command.Outputs, "output");

        var newEvent = new NewTraceabilityEvent(
            Guid.NewGuid(),
            command.EventTypeId,
            command.OrganizationId,
            command.LocationId,
            command.OccurredAt,
            command.ExternalReference,
            command.Description,
            command.CreatedBy,
            timeProvider.GetUtcNow(),
            command.Inputs
                .Select(input => new NewTraceabilityEventLot(
                    Guid.NewGuid(),
                    input.LotId,
                    input.Quantity))
                .ToArray(),
            command.Outputs
                .Select(output => new NewTraceabilityEventLot(
                    Guid.NewGuid(),
                    output.LotId,
                    output.Quantity))
                .ToArray());

        TraceabilityEvent traceabilityEvent;
        try
        {
            traceabilityEvent = await writer.AddAsync(
                newEvent,
                referencedLots => CreateDomainEvent(newEvent, referencedLots),
                cancellationToken);
        }
        catch (TraceabilityDomainException exception)
        {
            throw new TraceabilityEventValidationException(exception.Message);
        }

        return MapDetails(traceabilityEvent);
    }

    private static TraceabilityEvent CreateDomainEvent(
        NewTraceabilityEvent newEvent,
        IReadOnlyDictionary<Guid, ReferencedLotDetails> referencedLots)
    {
        var inputs = newEvent.Inputs
            .Select(input => EventInput.Create(
                input.Id,
                input.LotId,
                input.Quantity,
                referencedLots[input.LotId].UnitId))
            .ToArray();
        var outputs = newEvent.Outputs
            .Select(output => EventOutput.Create(
                output.Id,
                output.LotId,
                output.Quantity,
                referencedLots[output.LotId].UnitId))
            .ToArray();

        return TraceabilityEvent.Create(
            newEvent.Id,
            newEvent.EventTypeId,
            newEvent.OrganizationId,
            newEvent.LocationId,
            newEvent.OccurredAt,
            newEvent.ExternalReference,
            newEvent.Description,
            newEvent.CreatedBy,
            newEvent.CreatedAt,
            inputs,
            outputs);
    }

    private static void ValidateQuantities(
        IReadOnlyList<TraceabilityEventLotCommand> lines,
        string lineType)
    {
        foreach (var line in lines)
        {
            if (line is null)
            {
                throw new TraceabilityEventValidationException(
                    $"Traceability event {lineType}s must not contain null entries.");
            }

            if (line.Quantity <= 0)
            {
                throw new TraceabilityEventValidationException(
                    $"Event {lineType} quantity must be greater than zero.");
            }

            if (decimal.Round(line.Quantity, 6) != line.Quantity)
            {
                throw new TraceabilityEventValidationException(
                    $"Traceability event {lineType} quantity must not have more than 6 decimal places.");
            }

            if (Math.Abs(line.Quantity) > MaximumSupportedQuantity)
            {
                throw new TraceabilityEventValidationException(
                    $"Traceability event {lineType} quantity exceeds the supported range.");
            }
        }
    }

    private static TraceabilityEventDetails MapDetails(TraceabilityEvent traceabilityEvent) =>
        new(
            traceabilityEvent.Id,
            traceabilityEvent.EventTypeId,
            traceabilityEvent.OrganizationId,
            traceabilityEvent.LocationId,
            traceabilityEvent.OccurredAt,
            traceabilityEvent.ExternalReference,
            traceabilityEvent.Description,
            traceabilityEvent.CreatedBy,
            traceabilityEvent.CreatedAt,
            traceabilityEvent.Inputs
                .Select(input => new TraceabilityEventLotDetails(
                    input.LotId,
                    input.Quantity,
                    input.UnitId))
                .ToArray(),
            traceabilityEvent.Outputs
                .Select(output => new TraceabilityEventLotDetails(
                    output.LotId,
                    output.Quantity,
                    output.UnitId))
                .ToArray());
}
