namespace FoodTraceability.Api.Contracts.LotBlocks;

public sealed record LotBlockListResponse(
    IReadOnlyList<LotBlockListItemResponse> Items, int Page, int PageSize, long TotalCount);

/// <summary>A stored block decision; both release fields are null while the block is open.</summary>
public sealed record LotBlockListItemResponse(
    Guid Id, Guid LotId, string Reason, DateTimeOffset BlockedAt, Guid BlockedBy,
    DateTimeOffset? ReleasedAt, Guid? ReleasedBy);
