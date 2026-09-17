using FoodTraceability.Platform.Contracts.Traceability;

namespace FoodTraceability.Modules.Quality.Application.Samples;

public sealed class SampleQueryService(ISampleReader reader, ILotArticleReader lotReader)
{
    public async Task<SamplePage?> ListAsync(ListSamplesQuery query, CancellationToken cancellationToken)
    {
        if (await lotReader.FindArticleIdAsync(query.OrganizationId, query.LotId, cancellationToken) is null)
        {
            return null;
        }

        return await reader.ListAsync(query, cancellationToken);
    }
}
