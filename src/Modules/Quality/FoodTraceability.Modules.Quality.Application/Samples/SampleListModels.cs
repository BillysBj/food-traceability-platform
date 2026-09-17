using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.Samples;

public sealed record ListSamplesQuery(Guid OrganizationId, Guid LotId, int Page, int PageSize);

public sealed record SampleListItem(
    Guid Id,
    string SampleNumber,
    SampleStatus Status,
    DateTimeOffset TakenAt,
    Guid LotId,
    Guid LocationId,
    Guid TraceabilityEventId,
    DateTimeOffset CreatedAt);

public sealed record SamplePage(
    IReadOnlyList<SampleListItem> Items, int Page, int PageSize, long TotalCount);
