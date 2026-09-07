namespace FoodTraceability.Modules.Catalog.Application.Units;

public interface IUnitReader
{
    Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken);

    Task<string?> FindCodeByIdAsync(Guid unitId, CancellationToken cancellationToken);
}
