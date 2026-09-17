namespace FoodTraceability.Modules.Quality.Application.LabResults;

public interface ILabResultReader
{
    Task<LabResultPage> ListAsync(ListLabResultsQuery query, CancellationToken cancellationToken);
}
