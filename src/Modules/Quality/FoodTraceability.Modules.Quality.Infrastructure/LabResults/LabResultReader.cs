using FoodTraceability.Modules.Quality.Application.LabResults;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.LabResults;

internal sealed class LabResultReader(QualityDbContext dbContext) : ILabResultReader
{
    public async Task<LabResultPage> ListAsync(ListLabResultsQuery query, CancellationToken cancellationToken)
    {
        var results = dbContext.LabResults.AsNoTracking()
            .Where(result => result.SampleId == query.SampleId
                && dbContext.Samples.AsNoTracking().Any(sample => sample.Id == result.SampleId
                    && sample.OrganizationId == query.OrganizationId));
        var totalCount = await results.LongCountAsync(cancellationToken);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new LabResultPage([], query.Page, query.PageSize, totalCount);
        }

        var items = await results
            .OrderByDescending(result => result.CreatedAt)
            .ThenByDescending(result => result.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(result => new LabResultListItem(
                result.Id, result.SampleId, result.ParameterId, result.Value,
                result.Assessment, result.Method, result.MeasuredAt, result.CreatedAt))
            .ToListAsync(cancellationToken);
        return new LabResultPage(items, query.Page, query.PageSize, totalCount);
    }
}
