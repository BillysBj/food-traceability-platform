using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodTraceability.Modules.Identity.Infrastructure;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var environmentVariable = PlatformDbContextFactory.ConnectionStringEnvironmentVariable;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string is missing. Set the {environmentVariable} environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(connectionString, IdentityDbContext.Schema);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
