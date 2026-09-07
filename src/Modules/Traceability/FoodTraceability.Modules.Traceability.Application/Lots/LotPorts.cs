using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Modules.Traceability.Application.Lots;

public interface ILotReader
{
    Task<LotDetails?> FindByIdAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken);

    Task<LotPage> ListAsync(
        ListLotsQuery query,
        CancellationToken cancellationToken);
}

public interface ILotWriter
{
    Task AddAsync(Lot lot, CancellationToken cancellationToken);
}
