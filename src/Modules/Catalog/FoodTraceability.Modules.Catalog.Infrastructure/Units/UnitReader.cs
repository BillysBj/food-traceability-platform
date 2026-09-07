using FoodTraceability.Modules.Catalog.Application.Units;
using FoodTraceability.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Catalog.Infrastructure.Units;

internal sealed class UnitReader(CatalogDbContext dbContext) : IUnitReader
{
    public Task<Guid?> FindIdByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var unitCode = UnitCode.Create(code);
        return dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.Code == unitCode)
            .Select(unit => (Guid?)unit.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> FindCodeByIdAsync(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var unitCode = await dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.Id == unitId)
            .Select(unit => unit.Code)
            .SingleOrDefaultAsync(cancellationToken);
        return unitCode?.Value;
    }
}
