using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Quality.Infrastructure.LabResults;

internal sealed class LabResultWriter(QualityDbContext dbContext, ScopedTransaction transaction) : ILabResultWriter
{
    private const string SampleParameterUniqueIndex = "ux_lab_result_sample_id_parameter_id";
    private const string SampleForeignKey = "fk_lab_result_sample";
    private const string ParameterForeignKey = "fk_lab_result_parameter";

    public async Task<Sample?> FindSampleAsync(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken)
    {
        await transaction.EnlistAsync(dbContext, cancellationToken);
        return await dbContext.Samples.SingleOrDefaultAsync(
            sample => sample.Id == sampleId && sample.OrganizationId == organizationId,
            cancellationToken);
    }

    public async Task AddAsync(LabResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        await transaction.EnlistAsync(dbContext, cancellationToken);
        dbContext.LabResults.Add(result);
        try
        {
            // Persist only actual changes to the tracked sample together with the result.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: SampleParameterUniqueIndex,
            })
        {
            throw new LabResultConflictException("A result for this sample and parameter already exists.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation,
                ConstraintName: SampleForeignKey,
            })
        {
            throw new LabResultSampleNotFoundException();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation,
                ConstraintName: ParameterForeignKey,
            })
        {
            throw new LabResultValidationException("The referenced parameter does not exist.");
        }
    }
}
