using System.Data;
using FoodTraceability.Modules.Traceability.Application.Traces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Traces;

internal sealed class BackwardTraceReader(
    TraceabilityDbContext dbContext,
    IOptions<TraceGraphOptions> options) : IBackwardTraceReader
{
    public async Task<TraceGraphDetails?> ReadAsync(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        var maxNodes = options.Value.MaxNodes;
        // Keep the bound check, nodes and edges in one snapshot. Concurrent events must
        // not add ancestry between the bound check and the graph reads.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var lotIds = await dbContext.Database.SqlQuery<Guid>(
            $"""
            WITH RECURSIVE ancestors(lot_id) AS (
                SELECT lot_id FROM trace.lot
                 WHERE organization_id = {organizationId} AND lot_id = {lotId}
                -- UNION, not UNION ALL: deduplicate converging paths and terminate
                -- even if unexpected existing data contains a cycle.
                UNION
                SELECT i.lot_id
                  FROM ancestors a
                  JOIN trace.event_output o
                    ON o.lot_id = a.lot_id AND o.organization_id = {organizationId}
                  JOIN trace.traceability_event e
                    ON e.event_id = o.event_id AND e.organization_id = {organizationId}
                  JOIN trace.event_input i
                    ON i.event_id = e.event_id AND i.organization_id = {organizationId}
                  JOIN trace.lot l
                    ON l.lot_id = i.lot_id AND l.organization_id = {organizationId}
            )
            SELECT lot_id AS "Value" FROM ancestors LIMIT {(long)maxNodes + 1}
            """)
            .ToArrayAsync(cancellationToken);

        // The outer LIMIT consumes at most MaxNodes + 1 rows from PostgreSQL's
        // recursive CTE. Do not sort/join the CTE in the outer SELECT before LIMIT:
        // that could exhaust the traversal. No graph payload is loaded until this check passes.
        if (lotIds.Length > maxNodes)
        {
            throw new TraceTooLargeException(maxNodes);
        }

        if (lotIds.Length == 0)
        {
            return null;
        }

        var nodes = await dbContext.Database.SqlQuery<TraceNodeDetails>(
            $"""
            SELECT l.lot_id, l.lot_number,
                   l.article_id, l.quantity, l.unit_id
              FROM unnest({lotIds}) a(lot_id)
              JOIN trace.lot l ON l.lot_id = a.lot_id
             WHERE l.organization_id = {organizationId}
             ORDER BY l.lot_id
            """)
            .ToArrayAsync(cancellationToken);

        // Edges always point from INPUT to OUTPUT: ToLot descends from FromLot.
        var edges = await dbContext.Database.SqlQuery<TraceEdgeDetails>(
            $"""
            SELECT DISTINCT e.event_id, t.code AS event_type_code,
                   e.occurred_at, i.lot_id AS from_lot_id, o.lot_id AS to_lot_id
              FROM trace.event_output o
              JOIN trace.traceability_event e
                ON e.event_id = o.event_id AND e.organization_id = {organizationId}
              JOIN trace.event_input i
                ON i.event_id = e.event_id AND i.organization_id = {organizationId}
              JOIN trace.event_type t ON t.event_type_id = e.event_type_id
             WHERE o.organization_id = {organizationId}
               AND o.lot_id = ANY({lotIds}) AND i.lot_id = ANY({lotIds})
             ORDER BY e.event_id, i.lot_id, o.lot_id
            """)
            .ToArrayAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new TraceGraphDetails(lotId, nodes, edges);
    }
}
