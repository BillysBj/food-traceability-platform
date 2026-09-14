using FoodTraceability.Modules.Quality.Application.Samples;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Quality.Infrastructure.Samples;

internal sealed class SampleWriter(QualityDbContext dbContext, ScopedTransaction transaction)
    : ISampleWriter
{
    private const string SampleNumberUniqueIndex = "ux_sample_organization_id_sample_number_upper";

    public async Task AddAsync(Sample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);

        await transaction.EnlistAsync(dbContext, cancellationToken);
        dbContext.Samples.Add(sample);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: SampleNumberUniqueIndex,
            })
        {
            throw new SampleConflictException(
                "A sample with this sample number already exists in this organization.");
        }
    }
}
