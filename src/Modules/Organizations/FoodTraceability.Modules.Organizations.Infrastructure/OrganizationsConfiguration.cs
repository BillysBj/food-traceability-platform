using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Infrastructure.Organizations;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodTraceability.Modules.Organizations.Infrastructure;

public static class OrganizationsConfiguration
{
    public static IServiceCollection AddOrganizations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<OrganizationsDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            options.UseFoodTraceabilityPostgres(connectionString, OrganizationsDbContext.Schema);
        });
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOrganizationReader, OrganizationReader>();
        services.AddScoped<ILocationWriter, LocationWriter>();
        services.AddScoped<OrganizationQueryService>();
        services.AddScoped<CreateLocationService>();

        return services;
    }
}
