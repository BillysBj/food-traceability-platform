using FoodTraceability.Modules.Documents.Application;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FoodTraceability.Modules.Documents.Infrastructure;

public static class DocumentsConfiguration
{
    public static IServiceCollection AddDocuments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoodTraceabilityConnection();
        services.AddDbContext<DocumentsDbContext>((serviceProvider, options) =>
        {
            options.UseFoodTraceabilityPostgres(
                serviceProvider.GetRequiredService<NpgsqlConnection>(),
                DocumentsDbContext.Schema);
        });

        services.AddScoped<IDocumentContentStore, DocumentContentStore>();

        return services;
    }
}
