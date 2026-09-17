namespace FoodTraceability.Modules.Quality.Application.Samples;

public interface ISampleReader
{
    Task<bool> ExistsAsync(Guid organizationId, Guid sampleId, CancellationToken cancellationToken);

    Task<SamplePage> ListAsync(ListSamplesQuery query, CancellationToken cancellationToken);
}
