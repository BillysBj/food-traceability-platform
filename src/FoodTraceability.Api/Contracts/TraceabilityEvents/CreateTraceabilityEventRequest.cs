namespace FoodTraceability.Api.Contracts.TraceabilityEvents;

public sealed record CreateTraceabilityEventRequest(
    string? EventTypeCode,
    Guid? LocationId,
    DateTimeOffset? OccurredAt,
    string? ExternalReference,
    string? Description,
    IReadOnlyList<TraceabilityEventLotRequest>? Inputs,
    IReadOnlyList<TraceabilityEventLotRequest>? Outputs);

public sealed record TraceabilityEventLotRequest(Guid LotId, decimal Quantity);
