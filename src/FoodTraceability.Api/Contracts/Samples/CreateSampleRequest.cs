namespace FoodTraceability.Api.Contracts.Samples;

/// <summary>Sample metadata and the quantity withdrawn in the referenced lot's unit.</summary>
public sealed record CreateSampleRequest(
    string? SampleNumber,
    Guid LotId,
    Guid LocationId,
    decimal Quantity,
    DateTimeOffset? TakenAt);
