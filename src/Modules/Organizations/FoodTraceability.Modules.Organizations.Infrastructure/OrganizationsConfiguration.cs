using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Infrastructure.Organizations;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FoodTraceability.Modules.Organizations.Infrastructure;

public static class OrganizationsConfiguration
{
    public static IServiceCollection AddOrganizations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoodTraceabilityConnection();
        services.AddDbContext<OrganizationsDbContext>((serviceProvider, options) =>
        {
            options.UseFoodTraceabilityPostgres(
                serviceProvider.GetRequiredService<NpgsqlConnection>(),
                OrganizationsDbContext.Schema);
        });
        services.TryAddSingleton<TimeProvider>(MicrosecondTimeProvider.System);
        services.AddScoped<IOrganizationReader, OrganizationReader>();
        services.AddScoped<IOrganizationWriter, OrganizationWriter>();
        services.AddScoped<ILocationReader, LocationReader>();
        services.AddScoped<ILocationWriter, LocationWriter>();
        services.AddScoped<OrganizationQueryService>();
        services.AddScoped<LocationQueryService>();
        services.AddScoped<CreateOrganizationService>();
        services.AddScoped<CreateLocationService>();

        return services;
    }
}
