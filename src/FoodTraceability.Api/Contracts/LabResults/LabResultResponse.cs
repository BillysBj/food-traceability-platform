namespace FoodTraceability.Api.Contracts.LabResults;

/// <summary>The created laboratory result and the resulting sample status (PENDING, PASS or FAIL).</summary>
public sealed record LabResultResponse(
    Guid Id,
    Guid SampleId,
    Guid ParameterId,
    decimal Value,
    string Assessment,
    string Method,
    DateTimeOffset MeasuredAt,
    string SampleStatus);
