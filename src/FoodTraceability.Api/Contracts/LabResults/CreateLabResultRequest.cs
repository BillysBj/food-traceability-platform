namespace FoodTraceability.Api.Contracts.LabResults;

/// <summary>A numeric measurement, the laboratory's PASS/FAIL assessment and the method actually used.</summary>
/// <param name="ParameterId">Identifier of an existing quality parameter.</param>
/// <param name="Value">Required measurement fitting decimal precision 18 and scale 6 without rounding.</param>
/// <param name="Assessment">Exactly PASS or FAIL.</param>
/// <param name="Method">Required technical method designation, at most 128 characters after trimming.</param>
/// <param name="MeasuredAt">Required measurement time; stored and returned at microsecond precision.</param>
public sealed record CreateLabResultRequest(
    Guid ParameterId,
    decimal? Value,
    string? Assessment,
    string? Method,
    DateTimeOffset? MeasuredAt);
