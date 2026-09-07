namespace FoodTraceability.Modules.Catalog.Application.Units;

public interface IUnitReader
{
    Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken);

    Task<string?> FindCodeByIdAsync(Guid unitId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> FindCodesByIdsAsync(
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken);
}
