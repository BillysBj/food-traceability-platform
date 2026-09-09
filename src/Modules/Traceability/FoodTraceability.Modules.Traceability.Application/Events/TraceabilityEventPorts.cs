using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.Events;

public delegate TraceabilityEvent BuildTraceabilityEvent(
    IReadOnlyDictionary<Guid, ReferencedLotDetails> referencedLots);

public interface ITraceabilityEventWriter
{
    Task<TraceabilityEvent> AddAsync(
        NewTraceabilityEvent newEvent,
        BuildTraceabilityEvent buildEvent,
        CancellationToken cancellationToken);
}

public interface ITraceabilityEventReader
{
    Task<TraceabilityEventDetails?> FindByIdAsync(
        Guid organizationId,
        Guid eventId,
        CancellationToken cancellationToken);
}
