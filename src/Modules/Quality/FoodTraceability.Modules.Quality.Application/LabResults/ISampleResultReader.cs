using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public interface ISampleResultReader
{
    // Requires an active transaction. Lock the sample until transaction completion,
    // refresh the tracked sample, then read its persisted results under that lock.
    Task<IReadOnlyList<SampleResultAssessment>> LockAndReadAsync(
        Sample sample, CancellationToken cancellationToken);
}

public sealed record SampleResultAssessment(Guid ParameterId, LabResultAssessment Assessment);
