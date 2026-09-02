using FoodTraceability.Modules.Catalog.Application.Articles;
using FoodTraceability.Modules.Catalog.Infrastructure.Articles;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FoodTraceability.Modules.Catalog.Infrastructure;

public static class CatalogConfiguration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<CatalogDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            options.UseFoodTraceabilityPostgres(connectionString, CatalogDbContext.Schema);
        });
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IArticleReader, ArticleReader>();
        services.AddScoped<IArticleWriter, ArticleWriter>();
        services.AddScoped<ArticleQueryService>();
        services.AddScoped<CreateArticleService>();

        return services;
    }
}
