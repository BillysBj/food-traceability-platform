using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodTraceability.Modules.Catalog.Infrastructure;

public sealed class CatalogDbContextFactory
    : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var environmentVariable = PlatformDbContextFactory.ConnectionStringEnvironmentVariable;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string is missing. Set the {environmentVariable} environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(connectionString, CatalogDbContext.Schema);

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
