namespace FoodTraceability.Api.Contracts.Samples;

public sealed record SampleListResponse(
    IReadOnlyList<SampleListItemResponse> Items, int Page, int PageSize, long TotalCount);

/// <summary>A stored sample with its PENDING, PASS or FAIL status.</summary>
public sealed record SampleListItemResponse(
    Guid Id,
    string SampleNumber,
    string Status,
    DateTimeOffset TakenAt,
    Guid LotId,
    Guid LocationId,
    Guid TraceabilityEventId,
    DateTimeOffset CreatedAt);
