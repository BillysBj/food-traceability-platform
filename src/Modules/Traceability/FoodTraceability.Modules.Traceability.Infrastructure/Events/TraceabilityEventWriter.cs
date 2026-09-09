using System.Buffers.Binary;
using System.Security.Cryptography;
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

        // SHA-256 over the UUID in network byte order, first eight hash bytes interpreted
        // as a signed big-endian bigint. All UUID bits contribute to the stable key.
        // A hash collision only serializes unrelated organizations; it cannot break safety.
        var organizationLockKey = BinaryPrimitives.ReadInt64BigEndian(
            SHA256.HashData(newEvent.OrganizationId.ToByteArray(bigEndian: true)));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        // TRC-007's composite FKs bind every input/output lot to its event's organization.
        // Every ancestry edge therefore stays within one organization, and cycles cannot
        // cross that boundary. An organization-wide transaction lock also serializes
        // writers with disjoint lot sets that could jointly close a cycle.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({organizationLockKey})",
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

        // Validate the domain before database conflicts, so self-reference remains a
        // validation error even when the same request would also overconsume a lot.
        var referencedLotsById = referencedLots.ToDictionary(lot => lot.LotId);
        var traceabilityEvent = buildEvent(referencedLotsById);

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

        // Domain validation (including same-event self-reference) precedes the graph check.
        if (newEvent.Inputs.Count > 0 && newEvent.Outputs.Count > 0)
        {
            var inputLotIds = newEvent.Inputs.Select(input => input.LotId).ToArray();
            var outputLotIds = newEvent.Outputs.Select(output => output.LotId).ToArray();
            var cycles = await dbContext.Database.SqlQuery<int>(
                $"""
                WITH RECURSIVE ancestors(lot_id) AS (
                    SELECT unnest({inputLotIds})
                    -- UNION, not UNION ALL: terminate even if existing data contains a cycle.
                    UNION
                    SELECT i.lot_id
                      FROM ancestors a
                      JOIN trace.event_output o
                        ON o.lot_id = a.lot_id AND o.organization_id = {newEvent.OrganizationId}
                      JOIN trace.event_input i
                        ON i.event_id = o.event_id AND i.organization_id = {newEvent.OrganizationId}
                )
                SELECT 1 AS "Value" FROM ancestors WHERE lot_id = ANY({outputLotIds}) LIMIT 1
                """)
                .ToListAsync(cancellationToken);
            if (cycles.Count > 0)
            {
                throw new TraceabilityEventConflictException(
                    "An output lot is already an ancestor of an input lot; the event would create a cycle.");
            }
        }

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
