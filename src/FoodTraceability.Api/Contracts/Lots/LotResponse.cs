namespace FoodTraceability.Api.Contracts.Lots;

/// <summary>A lot including its current quality status: PENDING, BLOCKED or RELEASED.</summary>
public sealed record LotResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ArticleId,
    string LotNumber,
    decimal Quantity,
    string UnitCode,
    DateTimeOffset CreatedAt,
    string QualityStatus);
