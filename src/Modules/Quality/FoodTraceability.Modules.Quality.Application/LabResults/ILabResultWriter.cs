using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public interface ILabResultWriter
{
    // Returns a tracked sample only within this organization. Missing and foreign
    // samples are indistinguishable. Changes are saved together with AddAsync.
    Task<Sample?> FindSampleAsync(Guid organizationId, Guid sampleId, CancellationToken cancellationToken);

    // Persists the result and changes to the tracked sample in one SaveChanges.
    Task AddAsync(LabResult result, CancellationToken cancellationToken);
}
