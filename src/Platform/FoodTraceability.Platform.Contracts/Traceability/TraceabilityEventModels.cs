namespace FoodTraceability.Platform.Contracts.Traceability;

public sealed record CreateTraceabilityEventRequest(
    Guid OrganizationId,
    Guid EventTypeId,
    Guid LocationId,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    IReadOnlyList<TraceabilityEventLot>? Inputs,
    IReadOnlyList<TraceabilityEventLot>? Outputs);

public sealed record TraceabilityEventLot(Guid LotId, decimal Quantity);

/// <summary>
/// Identifies the created event and its persisted occurrence time, including storage precision.
/// </summary>
public sealed record CreateTraceabilityEventResult(Guid EventId, DateTimeOffset OccurredAt);
