namespace FoodTraceability.Api.Contracts.LabResults;

public sealed record LabResultListResponse(
    IReadOnlyList<LabResultListItemResponse> Items, int Page, int PageSize, long TotalCount);

/// <summary>A stored laboratory result with its PASS or FAIL assessment.</summary>
public sealed record LabResultListItemResponse(
    Guid Id,
    Guid SampleId,
    Guid ParameterId,
    decimal Value,
    string Assessment,
    string Method,
    DateTimeOffset MeasuredAt,
    DateTimeOffset CreatedAt);
