namespace FoodTraceability.Api.Contracts.Samples;

/// <summary>The created sample and its associated SAMPLE event.</summary>
public sealed record SampleResponse(
    Guid Id,
    string SampleNumber,
    string Status,
    DateTimeOffset TakenAt,
    Guid LotId,
    Guid LocationId,
    Guid TraceabilityEventId);
