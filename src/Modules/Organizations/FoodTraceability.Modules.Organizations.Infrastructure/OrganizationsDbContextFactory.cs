using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodTraceability.Modules.Organizations.Infrastructure;

public sealed class OrganizationsDbContextFactory
    : IDesignTimeDbContextFactory<OrganizationsDbContext>
{
    public OrganizationsDbContext CreateDbContext(string[] args)
    {
        var environmentVariable = PlatformDbContextFactory.ConnectionStringEnvironmentVariable;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string is missing. Set the {environmentVariable} environment variable before running Entity Framework Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(connectionString, OrganizationsDbContext.Schema);

        return new OrganizationsDbContext(optionsBuilder.Options);
    }
}
