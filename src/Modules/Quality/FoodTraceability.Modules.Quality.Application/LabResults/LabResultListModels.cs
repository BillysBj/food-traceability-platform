using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed record ListLabResultsQuery(Guid OrganizationId, Guid SampleId, int Page, int PageSize);

public sealed record LabResultListItem(
    Guid Id,
    Guid SampleId,
    Guid ParameterId,
    decimal Value,
    LabResultAssessment Assessment,
    string Method,
    DateTimeOffset MeasuredAt,
    DateTimeOffset CreatedAt);

public sealed record LabResultPage(
    IReadOnlyList<LabResultListItem> Items, int Page, int PageSize, long TotalCount);
