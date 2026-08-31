using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Platform.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string MigrationsHistorySchema = "public";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.UseFoodTraceabilityModelConventions();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCollation("en", locale: "en-US", provider: "icu", deterministic: true);
        modelBuilder.HasCollation("el", locale: "el-GR", provider: "icu", deterministic: true);
    }
}
