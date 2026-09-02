using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodTraceability.Modules.Traceability.Infrastructure;

public sealed class TraceabilityDbContextFactory
    : IDesignTimeDbContextFactory<TraceabilityDbContext>
{
    public TraceabilityDbContext CreateDbContext(string[] args)
    {
        var environmentVariable = PlatformDbContextFactory.ConnectionStringEnvironmentVariable;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string is missing. Set the {environmentVariable} environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<TraceabilityDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(connectionString, TraceabilityDbContext.Schema);

        return new TraceabilityDbContext(optionsBuilder.Options);
    }
}
