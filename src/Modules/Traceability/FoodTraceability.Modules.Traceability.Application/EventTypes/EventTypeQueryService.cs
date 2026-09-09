using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.EventTypes;

public sealed class EventTypeQueryService(IEventTypeReader reader)
{
    public Task<Guid?> FindIdByCodeAsync(
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
            return Task.FromResult<Guid?>(null);
        }

        return reader.FindIdByCodeAsync(eventTypeCode.Value, cancellationToken);
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
