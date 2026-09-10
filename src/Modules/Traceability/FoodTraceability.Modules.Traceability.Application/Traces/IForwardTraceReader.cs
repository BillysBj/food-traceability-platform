namespace FoodTraceability.Modules.Traceability.Application.Traces;

public interface IForwardTraceReader
{
    Task<TraceGraphDetails?> ReadAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken);
}
