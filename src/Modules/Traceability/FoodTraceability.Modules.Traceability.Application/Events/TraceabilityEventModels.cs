namespace FoodTraceability.Modules.Traceability.Application.Events;

public sealed record CreateTraceabilityEventCommand(
    Guid OrganizationId,
    string EventTypeCode,
    Guid LocationId,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    IReadOnlyList<TraceabilityEventLotCommand>? Inputs,
    IReadOnlyList<TraceabilityEventLotCommand>? Outputs);

public sealed record TraceabilityEventLotCommand(Guid LotId, decimal Quantity);

public sealed record NewTraceabilityEvent(
    Guid Id,
    Guid EventTypeId,
    Guid OrganizationId,
    Guid LocationId,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NewTraceabilityEventLot> Inputs,
    IReadOnlyList<NewTraceabilityEventLot> Outputs);

public sealed record NewTraceabilityEventLot(Guid Id, Guid LotId, decimal Quantity);

public sealed record ReferencedLotDetails(
    Guid LotId,
    decimal InitialQuantity,
    Guid UnitId);

public sealed record TraceabilityEventDetails(
    Guid Id,
    Guid EventTypeId,
    Guid OrganizationId,
    Guid LocationId,
    DateTimeOffset OccurredAt,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TraceabilityEventLotDetails> Inputs,
    IReadOnlyList<TraceabilityEventLotDetails> Outputs);

public sealed record TraceabilityEventLotDetails(
    Guid LotId,
    decimal Quantity,
    Guid UnitId);
