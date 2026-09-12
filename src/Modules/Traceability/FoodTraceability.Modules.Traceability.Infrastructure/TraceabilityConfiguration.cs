using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Traceability.Application.EventTypes;
using FoodTraceability.Modules.Traceability.Application.Events;
using FoodTraceability.Modules.Traceability.Application.Lots;
using FoodTraceability.Modules.Traceability.Application.Traces;
using FoodTraceability.Modules.Traceability.Infrastructure.EventTypes;
using FoodTraceability.Modules.Traceability.Infrastructure.Events;
using FoodTraceability.Modules.Traceability.Infrastructure.Lots;
using FoodTraceability.Modules.Traceability.Infrastructure.Traces;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FoodTraceability.Modules.Traceability.Infrastructure;

public static class TraceabilityConfiguration
{
    public static IServiceCollection AddTraceability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFoodTraceabilityConnection();
        services.AddDbContext<TraceabilityDbContext>((serviceProvider, options) =>
        {
            options.UseFoodTraceabilityPostgres(
                serviceProvider.GetRequiredService<NpgsqlConnection>(),
                TraceabilityDbContext.Schema);
        });
        services.TryAddSingleton<TimeProvider>(MicrosecondTimeProvider.System);
        services.AddScoped<IBackwardTraceReader, BackwardTraceReader>();
        services.AddScoped<BackwardTraceQueryService>();
        services.AddScoped<IForwardTraceReader, ForwardTraceReader>();
        services.AddScoped<ForwardTraceQueryService>();
        services.AddScoped<IEventTypeReader, EventTypeReader>();
        services.AddScoped<EventTypeQueryService>();
        services.AddScoped<ITraceabilityEventReader, TraceabilityEventReader>();
        services.AddScoped<ITraceabilityEventWriter, TraceabilityEventWriter>();
        services.AddScoped<TraceabilityEventQueryService>();
        services.AddScoped<CreateTraceabilityEventService>();
        services.AddScoped<ITraceabilityEventCreator, TraceabilityEventCreator>();
        services.AddScoped<ILotReader, LotReader>();
        services.AddScoped<ILotWriter, LotWriter>();
        services.AddScoped<LotQueryService>();
        services.AddScoped<CreateLotService>();

        return services;
    }
}
