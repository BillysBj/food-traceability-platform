namespace FoodTraceability.Api.Contracts.Organizations;

public sealed record LocationListResponse(
    IReadOnlyList<LocationResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);
