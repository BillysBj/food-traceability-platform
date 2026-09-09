namespace FoodTraceability.Api.Contracts.TraceabilityEvents;

public sealed record TraceabilityEventResponse(
    Guid Id,
    Guid OrganizationId,
    string EventTypeCode,
    Guid LocationId,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TraceabilityEventLotResponse> Inputs,
    IReadOnlyList<TraceabilityEventLotResponse> Outputs);

public sealed record TraceabilityEventLotResponse(
    Guid LotId,
    decimal Quantity,
    Guid UnitId);
