using FoodTraceability.Modules.Traceability.Application.EventTypes;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Traceability.Infrastructure.EventTypes;

internal sealed class EventTypeReader(TraceabilityDbContext dbContext) : IEventTypeReader
{
    public Task<Guid?> FindIdByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var eventTypeCode = EventTypeCode.Create(code);
        return dbContext.EventTypes
            .AsNoTracking()
            .Where(eventType => eventType.Code == eventTypeCode)
            .Select(eventType => (Guid?)eventType.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> FindCodeByIdAsync(
        Guid eventTypeId,
        CancellationToken cancellationToken)
    {
        var eventTypeCode = await dbContext.EventTypes
            .AsNoTracking()
            .Where(eventType => eventType.Id == eventTypeId)
            .Select(eventType => eventType.Code)
            .SingleOrDefaultAsync(cancellationToken);
        return eventTypeCode?.Value;
    }
}
