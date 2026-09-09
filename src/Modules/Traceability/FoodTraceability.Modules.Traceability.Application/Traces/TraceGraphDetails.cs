namespace FoodTraceability.Modules.Traceability.Application.Traces;

public sealed record TraceGraphDetails(
    Guid RootLotId,
    IReadOnlyList<TraceNodeDetails> Nodes,
    IReadOnlyList<TraceEdgeDetails> Edges);

public sealed record TraceNodeDetails(
    Guid LotId,
    string LotNumber,
    Guid ArticleId,
    decimal Quantity,
    Guid UnitId);

// FromLotId was an INPUT and ToLotId was an OUTPUT of the same event:
// "ToLot descends from FromLot", regardless of the traversal direction.
public sealed record TraceEdgeDetails(
    Guid EventId,
    string EventTypeCode,
    DateTimeOffset OccurredAt,
    Guid FromLotId,
    Guid ToLotId);
