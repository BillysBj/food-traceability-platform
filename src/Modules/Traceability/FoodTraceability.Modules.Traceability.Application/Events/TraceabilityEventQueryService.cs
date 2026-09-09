namespace FoodTraceability.Modules.Traceability.Application.Events;

public sealed class TraceabilityEventQueryService(ITraceabilityEventReader reader)
{
    public Task<TraceabilityEventDetails?> FindByIdAsync(
        Guid organizationId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return organizationId == Guid.Empty || eventId == Guid.Empty
            ? Task.FromResult<TraceabilityEventDetails?>(null)
            : reader.FindByIdAsync(organizationId, eventId, cancellationToken);
    }
}
