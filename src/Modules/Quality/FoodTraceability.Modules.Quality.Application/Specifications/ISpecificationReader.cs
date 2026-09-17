namespace FoodTraceability.Modules.Quality.Application.Specifications;

public interface ISpecificationReader
{
    Task<SampleSpecificationContext?> FindSampleAsync(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken);

    // Loads details by an already selected id; applicability belongs exclusively
    // to IApplicableSpecificationReader, shared with the PASS evaluation.
    Task<SpecificationDetails> GetAsync(Guid specificationId, CancellationToken cancellationToken);
}
