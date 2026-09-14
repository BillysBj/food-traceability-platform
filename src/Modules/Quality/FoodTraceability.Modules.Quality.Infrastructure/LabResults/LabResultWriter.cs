using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Quality.Infrastructure.LabResults;

internal sealed class LabResultWriter(QualityDbContext dbContext) : ILabResultWriter
{
    private const string SampleParameterUniqueIndex = "ux_lab_result_sample_id_parameter_id";
    private const string SampleForeignKey = "fk_lab_result_sample";
    private const string ParameterForeignKey = "fk_lab_result_parameter";

    public Task<Sample?> FindSampleAsync(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken) =>
        dbContext.Samples.SingleOrDefaultAsync(
            sample => sample.Id == sampleId && sample.OrganizationId == organizationId,
            cancellationToken);

    public async Task AddAsync(LabResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        dbContext.LabResults.Add(result);
        try
        {
            // EF tracks only the actual sample change. Do not call Update(sample):
            // PASS must never write a stale PENDING status over a concurrent FAIL.
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
