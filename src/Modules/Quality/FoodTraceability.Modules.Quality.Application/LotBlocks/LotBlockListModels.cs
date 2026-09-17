namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed record ListLotBlocksQuery(Guid OrganizationId, Guid LotId, int Page, int PageSize);

public sealed record LotBlockListItem(
    Guid Id, Guid LotId, string Reason, DateTimeOffset BlockedAt, Guid BlockedBy,
    DateTimeOffset? ReleasedAt, Guid? ReleasedBy);

public sealed record LotBlockPage(
    IReadOnlyList<LotBlockListItem> Items, int Page, int PageSize, long TotalCount);
