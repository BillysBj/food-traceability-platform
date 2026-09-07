namespace FoodTraceability.Api.Contracts.Lots;

public sealed record LotResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ArticleId,
    string LotNumber,
    decimal Quantity,
    string UnitCode,
    DateTimeOffset CreatedAt);
