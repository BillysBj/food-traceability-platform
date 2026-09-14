using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.Samples;

public sealed record CreateSampleCommand(
    Guid OrganizationId,
    string? SampleNumber,
    Guid LotId,
    Guid LocationId,
    decimal Quantity,
    DateTimeOffset TakenAt,
    Guid CreatedBy);

public sealed record SampleDetails(
    Guid Id,
    string SampleNumber,
    SampleStatus Status,
    DateTimeOffset TakenAt,
    Guid LotId,
    Guid LocationId,
    Guid TraceabilityEventId);
