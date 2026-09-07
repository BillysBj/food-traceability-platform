namespace FoodTraceability.Api.Contracts.Lots;

public sealed record LotListResponse(
    IReadOnlyList<LotResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);
