using FoodTraceability.Modules.Traceability.Application.Lots;
using FoodTraceability.Modules.Traceability.Infrastructure.Lots;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodTraceability.Modules.Traceability.Infrastructure;

public static class TraceabilityConfiguration
{
    public static IServiceCollection AddTraceability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<TraceabilityDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            options.UseFoodTraceabilityPostgres(
                connectionString,
                TraceabilityDbContext.Schema);
        });
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ILotReader, LotReader>();
        services.AddScoped<ILotWriter, LotWriter>();
        services.AddScoped<LotQueryService>();
        services.AddScoped<CreateLotService>();

        return services;
    }
}
