using FoodTraceability.Modules.Traceability.Application.Events;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Events;

internal sealed class TraceabilityEventWriter(TraceabilityDbContext dbContext)
    : ITraceabilityEventWriter
{
    private const string EventTypeForeignKey =
        "fk_traceability_event_trace_event_type";
    private const string LocationForeignKey =
        "fk_traceability_event_org_location";
    private const string InputLotForeignKey = "fk_event_input_trace_lot";
    private const string OutputLotForeignKey = "fk_event_output_trace_lot";

    public async Task<TraceabilityEvent> AddAsync(
        NewTraceabilityEvent newEvent,
        BuildTraceabilityEvent buildEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newEvent);
        ArgumentNullException.ThrowIfNull(buildEvent);

        var lotIds = newEvent.Inputs
            .Select(input => input.LotId)
            .Concat(newEvent.Outputs.Select(output => output.LotId))
            .Distinct()
            .ToArray();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        // Every writer that books inputs must lock the referenced lot rows first. Ordering
        // every input and output lot by id gives concurrent events the same lock order and
        // prevents requests that list the same lots differently from deadlocking each other.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT 1
              FROM trace.lot
             WHERE organization_id = {newEvent.OrganizationId}
               AND lot_id = ANY({lotIds})
             ORDER BY lot_id
               FOR UPDATE
            """,
            cancellationToken);

        var referencedLots = await dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.OrganizationId == newEvent.OrganizationId
                && lotIds.Contains(lot.Id))
            .Select(lot => new ReferencedLotDetails(
                lot.Id,
                lot.Quantity,
                lot.UnitId))
            .ToListAsync(cancellationToken);
        if (referencedLots.Count != lotIds.Length)
        {
            throw new TraceabilityEventValidationException(
                "One or more referenced lots do not exist in this organization.");
        }

        var bookedInputQuantities = await dbContext.Set<EventInput>()
            .AsNoTracking()
            .Where(input => lotIds.Contains(input.LotId)
                && EF.Property<Guid>(input, "OrganizationId") == newEvent.OrganizationId)
            .GroupBy(input => input.LotId)
            .Select(group => new
            {
                LotId = group.Key,
                Quantity = group.Sum(input => input.Quantity),
            })
            .ToDictionaryAsync(
                booked => booked.LotId,
                booked => booked.Quantity,
                cancellationToken);

        var referencedLotsById = referencedLots.ToDictionary(lot => lot.LotId);
        foreach (var input in newEvent.Inputs)
        {
            var lot = referencedLotsById[input.LotId];
            var alreadyBooked = bookedInputQuantities.GetValueOrDefault(input.LotId);
            var availableQuantity = lot.InitialQuantity - alreadyBooked;
            if (input.Quantity > availableQuantity)
            {
                throw new TraceabilityEventConflictException(
                    $"Lot '{input.LotId}' does not have sufficient available quantity.");
            }
        }

        var traceabilityEvent = buildEvent(referencedLotsById);
        dbContext.TraceabilityEvents.Add(traceabilityEvent);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                EventTypeForeignKey))
        {
            throw new TraceabilityEventValidationException(
                "The referenced event type does not exist.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                exception,
                PostgresErrorCodes.ForeignKeyViolation,
                LocationForeignKey))
        {
            throw new TraceabilityEventValidationException(
                "The referenced location does not exist in this organization.");
        }
        catch (DbUpdateException exception)
            when (IsConstraintViolation(
                    exception,
                    PostgresErrorCodes.ForeignKeyViolation,
                    InputLotForeignKey)
                || IsConstraintViolation(
                    exception,
                    PostgresErrorCodes.ForeignKeyViolation,
                    OutputLotForeignKey))
        {
            throw new TraceabilityEventValidationException(
                "One or more referenced lots do not exist in this organization.");
        }

        await transaction.CommitAsync(cancellationToken);
        return traceabilityEvent;
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
