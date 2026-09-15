using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed class CreateLabResultService(
    ILabResultWriter writer,
    TimeProvider timeProvider,
    IApplicationTransaction transaction,
    ISampleResultReader resultReader,
    ILotArticleReader lotArticleReader,
    IApplicableSpecificationReader specificationReader)
{
    public async Task<LabResultDetails> CreateAsync(
        CreateLabResultCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var handle = await transaction.BeginAsync(cancellationToken);
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

        if (result.Assessment == LabResultAssessment.Fail)
        {
            sample.Fail();
        }
        else if (sample.Status == SampleStatus.Pending)
        {
            var existingResults = await resultReader.LockAndReadAsync(sample, cancellationToken);
            // LockAndRead refreshes the sample after waiting for concurrent writers.
            if (sample.Status == SampleStatus.Pending)
            {
                await EvaluatePassAsync(sample, result, existingResults, cancellationToken);
            }
        }

        await writer.AddAsync(result, cancellationToken);
        await handle.CommitAsync(cancellationToken);

        return new LabResultDetails(
            result.Id, result.SampleId, result.ParameterId, result.Value,
            result.Assessment, result.Method, result.MeasuredAt, sample.Status);
    }

    private async Task EvaluatePassAsync(
        Sample sample,
        LabResult result,
        IReadOnlyList<SampleResultAssessment> existingResults,
        CancellationToken cancellationToken)
    {
        var articleId = await lotArticleReader.FindArticleIdAsync(
            sample.OrganizationId, sample.LotId, cancellationToken);
        if (articleId is null)
        {
            return;
        }

        var specifications = await specificationReader.FindAsync(articleId.Value, sample.TakenAt, cancellationToken);
        if (specifications.Count > 1)
        {
            throw new AmbiguousSpecificationException();
        }
        if (specifications.Count == 0)
        {
            return;
        }

        var passedParameterIds = existingResults
            .Where(existing => existing.Assessment == LabResultAssessment.Pass)
            .Select(existing => existing.ParameterId)
            .ToHashSet();
        // The new PASS result is not persisted yet and must count in this evaluation.
        passedParameterIds.Add(result.ParameterId);
        if (specifications[0].RequiredParameterIds.All(passedParameterIds.Contains))
        {
            sample.Pass();
        }
    }
}
