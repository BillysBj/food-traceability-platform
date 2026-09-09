using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.EventTypes;

public sealed class EventTypeQueryService(IEventTypeReader reader)
{
    public Task<EventTypeLookup?> FindByCodeAsync(
        string? code,
        CancellationToken cancellationToken)
    {
        EventTypeCode eventTypeCode;
        try
        {
            eventTypeCode = EventTypeCode.Create(code);
        }
        catch (TraceabilityDomainException)
        {
            return Task.FromResult<EventTypeLookup?>(null);
        }

        return reader.FindByCodeAsync(eventTypeCode.Value, cancellationToken);
    }

    public Task<string?> FindCodeByIdAsync(
        Guid eventTypeId,
        CancellationToken cancellationToken)
    {
        return eventTypeId == Guid.Empty
            ? Task.FromResult<string?>(null)
            : reader.FindCodeByIdAsync(eventTypeId, cancellationToken);
    }
}
