namespace FoodTraceability.Modules.Traceability.Application.Traces;

public sealed class ForwardTraceQueryService(IForwardTraceReader reader)
{
    public Task<TraceGraphDetails?> ReadAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken) =>
        organizationId == Guid.Empty || lotId == Guid.Empty
            ? Task.FromResult<TraceGraphDetails?>(null)
            : reader.ReadAsync(organizationId, lotId, cancellationToken);
}
