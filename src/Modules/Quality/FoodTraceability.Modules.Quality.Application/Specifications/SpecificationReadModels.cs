namespace FoodTraceability.Modules.Quality.Application.Specifications;

public sealed record SampleSpecificationContext(Guid LotId, DateTimeOffset TakenAt);

public sealed record SpecificationDetails(
    Guid Id, Guid ArticleId, int Version, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo,
    IReadOnlyList<SpecificationParameterDetails> Parameters);

public sealed record SpecificationParameterDetails(
    Guid ParameterId, string ParameterCode, string StandardMethod, Guid? UnitId,
    decimal? Minimum, decimal? Maximum, decimal? Target, bool Required);
