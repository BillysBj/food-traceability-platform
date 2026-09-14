using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed class CreateLabResultService(ILabResultWriter writer, TimeProvider timeProvider)
{
    public async Task<LabResultDetails> CreateAsync(
        CreateLabResultCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sample = await writer.FindSampleAsync(command.OrganizationId, command.SampleId, cancellationToken)
            ?? throw new LabResultSampleNotFoundException();

        LabResult result;
        try
        {
            result = LabResult.Create(
                Guid.NewGuid(), sample.Id, command.ParameterId, command.Value,
                command.Assessment, command.Method,
                TimestampPrecision.TruncateToMicroseconds(command.MeasuredAt),
                timeProvider.GetUtcNow());
        }
        catch (QualityDomainException exception)
        {
            throw new LabResultValidationException(exception.Message);
        }

        // Never touch Status for PASS: a concurrent FAIL must not be overwritten.
        if (result.Assessment == LabResultAssessment.Fail)
        {
            sample.Fail();
        }

        await writer.AddAsync(result, cancellationToken);

        return new LabResultDetails(
            result.Id, result.SampleId, result.ParameterId, result.Value,
            result.Assessment, result.Method, result.MeasuredAt, sample.Status);
    }
}
