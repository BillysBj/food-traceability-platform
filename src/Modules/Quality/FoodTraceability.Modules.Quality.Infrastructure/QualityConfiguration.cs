using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.Modules.Quality.Infrastructure;

public static class QualityConfiguration
{
    public static IServiceCollection AddQuality(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<QualityDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            options.UseFoodTraceabilityPostgres(connectionString, QualityDbContext.Schema);
        });

        return services;
    }
}
