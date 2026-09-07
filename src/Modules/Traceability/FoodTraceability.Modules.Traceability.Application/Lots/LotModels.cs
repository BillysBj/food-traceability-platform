namespace FoodTraceability.Modules.Traceability.Application.Lots;

public sealed record CreateLotCommand(
    Guid OrganizationId,
    Guid ArticleId,
    string? LotNumber,
    decimal Quantity,
    Guid UnitId);

public sealed record LotDetails(
    Guid Id,
    Guid OrganizationId,
    Guid ArticleId,
    string LotNumber,
    decimal Quantity,
    Guid UnitId,
    DateTimeOffset CreatedAt);

public sealed record ListLotsQuery(
    Guid OrganizationId,
    int Page,
    int PageSize,
    Guid? ArticleId,
    string? LotNumber);

public sealed record LotPage(
    IReadOnlyList<LotDetails> Items,
    int Page,
    int PageSize,
    long TotalCount);
