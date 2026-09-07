using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Modules.Catalog.Application.Units;

public sealed class UnitQueryService(IUnitReader reader)
{
    public Task<Guid?> FindIdByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        UnitCode unitCode;
        try
        {
            unitCode = UnitCode.Create(code);
        }
        catch (CatalogDomainException)
        {
            return Task.FromResult<Guid?>(null);
        }

        return reader.FindIdByCodeAsync(unitCode.Value, cancellationToken);
    }

    public Task<string?> FindCodeByIdAsync(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        return unitId == Guid.Empty
            ? Task.FromResult<string?>(null)
            : reader.FindCodeByIdAsync(unitId, cancellationToken);
    }

    public Task<IReadOnlyDictionary<Guid, string>> FindCodesByIdsAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken)
    {
        return unitIds.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                new Dictionary<Guid, string>())
            : reader.FindCodesByIdsAsync(unitIds, cancellationToken);
    }
}
