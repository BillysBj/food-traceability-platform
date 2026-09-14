using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.Modules.Quality.Application.Samples;

public interface ISampleWriter
{
    Task AddAsync(Sample sample, CancellationToken cancellationToken);
}
