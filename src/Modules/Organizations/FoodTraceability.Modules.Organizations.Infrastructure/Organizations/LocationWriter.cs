using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Organizations;

internal sealed class LocationWriter(OrganizationsDbContext dbContext) : ILocationWriter
{
    public async Task AddAsync(
        Location location,
        CancellationToken cancellationToken)
    {
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
