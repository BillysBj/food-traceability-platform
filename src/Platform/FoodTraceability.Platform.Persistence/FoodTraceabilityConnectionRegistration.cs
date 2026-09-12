using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FoodTraceability.Platform.Persistence;

public static class FoodTraceabilityConnectionRegistration
{
    public static IServiceCollection AddFoodTraceabilityConnection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<NpgsqlConnection>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            return new NpgsqlConnection(connectionString);
        });

        return services;
    }
}
