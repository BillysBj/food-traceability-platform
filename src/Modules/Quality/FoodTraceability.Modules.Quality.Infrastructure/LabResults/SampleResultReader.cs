using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure.LabResults;

internal sealed class SampleResultReader(QualityDbContext dbContext, ScopedTransaction transaction)
    : ISampleResultReader
{
    public async Task<IReadOnlyList<SampleResultAssessment>> LockAndReadAsync(
        Sample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (!transaction.IsActive)
        {
            throw new InvalidOperationException("Sample evaluation requires an active application transaction.");
        }

        await transaction.EnlistAsync(dbContext, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT 1
              FROM quality.sample
             WHERE organization_id = {sample.OrganizationId}
               AND sample_id = {sample.Id}
               FOR UPDATE
            """,
            cancellationToken);

        // The initial read may predate another writer's commit. In particular, never
        // overwrite a concurrent FAIL using the previously tracked PENDING status.
        await dbContext.Entry(sample).ReloadAsync(cancellationToken);
        if (dbContext.Entry(sample).State == EntityState.Detached)
        {
            throw new LabResultSampleNotFoundException();
        }

        return await dbContext.LabResults.AsNoTracking()
            .Where(result => result.SampleId == sample.Id)
            .Select(result => new SampleResultAssessment(result.ParameterId, result.Assessment))
            .ToListAsync(cancellationToken);
    }
}
