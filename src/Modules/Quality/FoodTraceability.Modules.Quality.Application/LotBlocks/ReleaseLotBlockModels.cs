using FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed record ReleaseLotBlockCommand(Guid OrganizationId, Guid LotId, Guid BlockId, Guid ReleasedBy);

public sealed record ReleasedLotBlockDetails(
    Guid Id,
    Guid LotId,
    string Reason,
    DateTimeOffset BlockedAt,
    Guid BlockedBy,
    DateTimeOffset ReleasedAt,
    Guid ReleasedBy,
    LotQualityStatus QualityStatus);
