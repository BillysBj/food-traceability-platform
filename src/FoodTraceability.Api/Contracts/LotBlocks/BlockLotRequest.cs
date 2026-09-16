namespace FoodTraceability.Api.Contracts.LotBlocks;

/// <summary>The reason for the explicit decision to block a lot.</summary>
/// <param name="Reason">Required nonblank reason, at most 2000 characters after trimming.</param>
public sealed record BlockLotRequest(string? Reason);
