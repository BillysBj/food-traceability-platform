using FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed record BlockLotCommand(Guid OrganizationId, Guid LotId, string? Reason, Guid BlockedBy);

public sealed record LotBlockDetails(
    Guid Id,
    Guid LotId,
    string Reason,
    DateTimeOffset BlockedAt,
    Guid BlockedBy,
    LotQualityStatus QualityStatus);
