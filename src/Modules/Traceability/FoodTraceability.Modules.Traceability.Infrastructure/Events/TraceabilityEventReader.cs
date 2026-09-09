using FoodTraceability.Modules.Traceability.Application.Events;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Traceability.Infrastructure.Events;

internal sealed class TraceabilityEventReader(TraceabilityDbContext dbContext)
    : ITraceabilityEventReader
{
    public async Task<TraceabilityEventDetails?> FindByIdAsync(
        Guid organizationId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var traceabilityEvent = await dbContext.TraceabilityEvents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Inputs)
            .Include(candidate => candidate.Outputs)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == eventId
                    && candidate.OrganizationId == organizationId,
                cancellationToken);
        if (traceabilityEvent is null)
        {
            return null;
        }

        return new TraceabilityEventDetails(
            traceabilityEvent.Id,
            traceabilityEvent.EventTypeId,
            traceabilityEvent.OrganizationId,
            traceabilityEvent.LocationId,
            traceabilityEvent.OccurredAt,
            traceabilityEvent.ExternalReference,
            traceabilityEvent.Description,
            traceabilityEvent.CreatedBy,
            traceabilityEvent.CreatedAt,
            traceabilityEvent.Inputs
                .OrderBy(input => input.Id)
                .Select(input => new TraceabilityEventLotDetails(
                    input.LotId,
                    input.Quantity,
                    input.UnitId))
                .ToArray(),
            traceabilityEvent.Outputs
                .OrderBy(output => output.Id)
                .Select(output => new TraceabilityEventLotDetails(
                    output.LotId,
                    output.Quantity,
                    output.UnitId))
                .ToArray());
    }
}
