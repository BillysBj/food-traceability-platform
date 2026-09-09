using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.EventTypes;

public sealed record EventTypeLookup(Guid Id, EventTypeClassification Classification);

public interface IEventTypeReader
{
    Task<EventTypeLookup?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    Task<string?> FindCodeByIdAsync(Guid eventTypeId, CancellationToken cancellationToken);
}
