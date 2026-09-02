using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Catalog.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Unit> Units => Set<Unit>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.UseFoodTraceabilityModelConventions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
