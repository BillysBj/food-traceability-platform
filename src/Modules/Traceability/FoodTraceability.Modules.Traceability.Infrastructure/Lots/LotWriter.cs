using FoodTraceability.Modules.Traceability.Application.Lots;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Lots;

internal sealed class LotWriter(TraceabilityDbContext dbContext) : ILotWriter
{
    private const string LotNumberUniqueIndex =
        "ux_lot_organization_id_lot_number_upper";
    private const string ArticleForeignKey = "fk_lot_catalog_article";
    private const string UnitForeignKey = "fk_lot_catalog_unit";

    public async Task AddAsync(Lot lot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lot);

        dbContext.Lots.Add(lot);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.UniqueViolation,
                LotNumberUniqueIndex))
        {
            throw new LotConflictException(
                "A lot with the same lot number already exists in this organization.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                ArticleForeignKey))
        {
            throw new LotValidationException("The referenced article does not exist.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                UnitForeignKey))
        {
            throw new LotValidationException("The referenced unit does not exist.");
        }
    }

    private static bool IsConstraintViolation(
        DbUpdateException exception,
        string sqlState,
        string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == sqlState
            && postgresException.ConstraintName == constraintName;
    }
}
