namespace FoodTraceability.Api.Contracts.LotBlocks;

/// <summary>The open block, its decision maker and time, and resulting lot quality status BLOCKED.</summary>
public sealed record LotBlockResponse(
    Guid Id,
    Guid LotId,
    string Reason,
    DateTimeOffset BlockedAt,
    Guid BlockedBy,
    string QualityStatus);
