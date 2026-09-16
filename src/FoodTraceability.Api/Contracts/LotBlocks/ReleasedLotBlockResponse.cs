namespace FoodTraceability.Api.Contracts.LotBlocks;

/// <summary>The block, its original decision and one-time release, and resulting lot quality status RELEASED.</summary>
public sealed record ReleasedLotBlockResponse(
    Guid Id,
    Guid LotId,
    string Reason,
    DateTimeOffset BlockedAt,
    Guid BlockedBy,
    DateTimeOffset ReleasedAt,
    Guid ReleasedBy,
    string QualityStatus);
