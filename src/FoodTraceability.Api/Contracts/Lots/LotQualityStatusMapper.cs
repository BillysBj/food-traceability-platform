using FoodTraceability.Platform.Contracts.Traceability;
using DomainStatus = FoodTraceability.Modules.Traceability.Domain.LotQualityStatus;

namespace FoodTraceability.Api.Contracts.Lots;

internal static class LotQualityStatusMapper
{
    public static string ToCode(DomainStatus status) => ToCode(status switch
    {
        DomainStatus.Pending => LotQualityStatus.Pending,
        DomainStatus.Blocked => LotQualityStatus.Blocked,
        DomainStatus.Released => LotQualityStatus.Released,
        _ => throw new InvalidOperationException($"Unexpected lot quality status '{status}'."),
    });

    public static string ToCode(LotQualityStatus status) => status switch
    {
        LotQualityStatus.Pending => "PENDING",
        LotQualityStatus.Blocked => "BLOCKED",
        LotQualityStatus.Released => "RELEASED",
        _ => throw new InvalidOperationException($"Unexpected lot quality status '{status}'."),
    };
}
