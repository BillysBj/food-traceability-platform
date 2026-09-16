using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Application.Samples;
using FoodTraceability.Modules.Quality.Infrastructure.LabResults;
using FoodTraceability.Modules.Quality.Infrastructure.LotBlocks;
using FoodTraceability.Modules.Quality.Infrastructure.Samples;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FoodTraceability.Modules.Quality.Infrastructure;

public static class QualityConfiguration
{
    public static IServiceCollection AddQuality(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoodTraceabilityConnection();
        services.AddDbContext<QualityDbContext>((serviceProvider, options) =>
        {
            options.UseFoodTraceabilityPostgres(
                serviceProvider.GetRequiredService<NpgsqlConnection>(),
                QualityDbContext.Schema);
        });

        services.AddScoped<ISampleWriter, SampleWriter>();
        services.AddScoped<CreateSampleService>();
        services.AddScoped<ILabResultWriter, LabResultWriter>();
        services.AddScoped<ISampleResultReader, SampleResultReader>();
        services.AddScoped<IApplicableSpecificationReader, ApplicableSpecificationReader>();
        services.AddScoped<CreateLabResultService>();
        services.AddScoped<ILotBlockWriter, LotBlockWriter>();
        services.AddScoped<BlockLotService>();
        services.AddScoped<ReleaseLotBlockService>();

        return services;
    }
}
