namespace FoodTraceability.Modules.Traceability.Application.Lots;

public sealed class LotQueryService(ILotReader reader)
{
    public Task<LotDetails?> FindByIdAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        return organizationId == Guid.Empty || lotId == Guid.Empty
            ? Task.FromResult<LotDetails?>(null)
            : reader.FindByIdAsync(organizationId, lotId, cancellationToken);
    }
}
