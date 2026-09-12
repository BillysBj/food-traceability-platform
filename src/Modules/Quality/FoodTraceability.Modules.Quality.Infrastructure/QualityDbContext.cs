using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Quality.Infrastructure;

public sealed class QualityDbContext(DbContextOptions<QualityDbContext> options)
    : DbContext(options)
{
    public const string Schema = "quality";

    public DbSet<Parameter> Parameters => Set<Parameter>();

    public DbSet<Sample> Samples => Set<Sample>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.UseFoodTraceabilityModelConventions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QualityDbContext).Assembly);
    }
}
