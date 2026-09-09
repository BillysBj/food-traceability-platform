namespace FoodTraceability.Api.Contracts.Traces;

/// <summary>A complete trace graph, including its root lot, with unique nodes and edges.</summary>
public sealed record TraceGraphResponse(
    Guid RootLotId,
    IReadOnlyList<TraceNodeResponse> Nodes,
    IReadOnlyList<TraceEdgeResponse> Edges);

/// <summary>A lot and its unchanged initial quantity (D-32).</summary>
public sealed record TraceNodeResponse(
    Guid LotId,
    string LotNumber,
    Guid ArticleId,
    decimal Quantity,
    string UnitCode);

/// <summary>FromLotId was an input and ToLotId an output of EventId: ToLot descends from FromLot.</summary>
public sealed record TraceEdgeResponse(
    Guid EventId,
    string EventTypeCode,
    DateTimeOffset OccurredAt,
    Guid FromLotId,
    Guid ToLotId);
