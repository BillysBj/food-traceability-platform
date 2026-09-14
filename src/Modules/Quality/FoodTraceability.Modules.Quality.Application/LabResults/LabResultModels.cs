using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed record CreateLabResultCommand(
    Guid OrganizationId,
    Guid SampleId,
    Guid ParameterId,
    decimal Value,
    LabResultAssessment Assessment,
    string? Method,
    DateTimeOffset MeasuredAt);

public sealed record LabResultDetails(
    Guid Id,
    Guid SampleId,
    Guid ParameterId,
    decimal Value,
    LabResultAssessment Assessment,
    string Method,
    DateTimeOffset MeasuredAt,
    SampleStatus SampleStatus);
