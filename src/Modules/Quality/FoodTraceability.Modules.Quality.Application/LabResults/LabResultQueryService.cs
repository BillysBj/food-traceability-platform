using FoodTraceability.Modules.Quality.Application.Samples;

namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed class LabResultQueryService(ILabResultReader reader, ISampleReader sampleReader)
{
    public async Task<LabResultPage?> ListAsync(ListLabResultsQuery query, CancellationToken cancellationToken)
    {
        if (!await sampleReader.ExistsAsync(query.OrganizationId, query.SampleId, cancellationToken))
        {
            return null;
        }

        return await reader.ListAsync(query, cancellationToken);
    }
}
