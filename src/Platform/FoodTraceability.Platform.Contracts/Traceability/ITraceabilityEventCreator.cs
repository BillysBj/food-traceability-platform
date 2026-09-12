namespace FoodTraceability.Platform.Contracts.Traceability;

public interface ITraceabilityEventCreator
{
    Task<CreateTraceabilityEventResult> CreateAsync(
        CreateTraceabilityEventRequest request,
        CancellationToken cancellationToken);
}
