using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Traceability.Infrastructure;

public sealed class TraceabilityDbContext(DbContextOptions<TraceabilityDbContext> options)
    : DbContext(options)
{
    public const string Schema = "trace";

    public DbSet<Lot> Lots => Set<Lot>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.UseFoodTraceabilityModelConventions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TraceabilityDbContext).Assembly);
    }
}
