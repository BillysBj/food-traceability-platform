using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodTraceability.Modules.Quality.Infrastructure;

public sealed class QualityDbContextFactory
    : IDesignTimeDbContextFactory<QualityDbContext>
{
    public QualityDbContext CreateDbContext(string[] args)
    {
        var environmentVariable = PlatformDbContextFactory.ConnectionStringEnvironmentVariable;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string is missing. Set the {environmentVariable} environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<QualityDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(connectionString, QualityDbContext.Schema);

        return new QualityDbContext(optionsBuilder.Options);
    }
}
