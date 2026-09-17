namespace FoodTraceability.Api.Contracts.Specifications;

/// <summary>The article specification applicable at the sampling time, including all parameters.</summary>
public sealed record SampleSpecificationResponse(
    Guid Id, Guid ArticleId, int Version, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo,
    IReadOnlyList<SpecificationParameterResponse> Parameters);

/// <summary>Reference limits for the laboratory; the system does not compare measurements against them.</summary>
public sealed record SpecificationParameterResponse(
    Guid ParameterId, string ParameterCode, string StandardMethod, Guid? UnitId,
    decimal? Minimum, decimal? Maximum, decimal? Target, bool Required);
