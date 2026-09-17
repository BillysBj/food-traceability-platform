using FoodTraceability.Modules.Quality.Application.Samples;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.Samples;

internal sealed class SampleReader(QualityDbContext dbContext) : ISampleReader
{
    public Task<bool> ExistsAsync(Guid organizationId, Guid sampleId, CancellationToken cancellationToken) =>
        dbContext.Samples.AsNoTracking().AnyAsync(
            sample => sample.OrganizationId == organizationId && sample.Id == sampleId, cancellationToken);

    public async Task<SamplePage> ListAsync(ListSamplesQuery query, CancellationToken cancellationToken)
    {
        var samples = dbContext.Samples.AsNoTracking()
            .Where(sample => sample.OrganizationId == query.OrganizationId && sample.LotId == query.LotId);
        var totalCount = await samples.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new SamplePage([], query.Page, query.PageSize, totalCount);
        }

        var items = await samples
            .OrderByDescending(sample => sample.CreatedAt)
            .ThenByDescending(sample => sample.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(sample => new SampleListItem(
                sample.Id, sample.SampleNumber, sample.Status, sample.TakenAt,
                sample.LotId, sample.LocationId, sample.TraceabilityEventId, sample.CreatedAt))
            .ToListAsync(cancellationToken);
        return new SamplePage(items, query.Page, query.PageSize, totalCount);
    }
}
