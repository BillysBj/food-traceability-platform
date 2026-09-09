namespace FoodTraceability.Modules.Traceability.Application.Traces;

public interface IBackwardTraceReader
{
    Task<TraceGraphDetails?> ReadAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken);
}
