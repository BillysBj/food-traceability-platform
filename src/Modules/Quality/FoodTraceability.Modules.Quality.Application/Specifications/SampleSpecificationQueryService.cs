using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Quality.Application.Specifications;

public sealed class SampleSpecificationQueryService(
    ISpecificationReader reader,
    ILotArticleReader lotReader,
    IApplicableSpecificationReader applicableReader)
{
    public async Task<SpecificationDetails?> FindAsync(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken)
    {
        var sample = await reader.FindSampleAsync(organizationId, sampleId, cancellationToken)
            ?? throw new SpecificationSampleNotFoundException();
        var articleId = await lotReader.FindArticleIdAsync(organizationId, sample.LotId, cancellationToken)
            ?? throw new SpecificationSampleNotFoundException();

        // Use the QLT-004 selector unchanged: article, sampling time, inclusive
        // boundaries and an open end have exactly one implementation.
        var specifications = await applicableReader.FindAsync(articleId, sample.TakenAt, cancellationToken);
        if (specifications.Count > 1)
        {
            throw new AmbiguousSpecificationException();
        }

        return specifications.Count == 0
            ? null
            : await reader.GetAsync(specifications[0].Id, cancellationToken);
    }
}

public sealed class SpecificationSampleNotFoundException : Exception
{
    public SpecificationSampleNotFoundException() : base("Sample not found.")
    {
    }
}
