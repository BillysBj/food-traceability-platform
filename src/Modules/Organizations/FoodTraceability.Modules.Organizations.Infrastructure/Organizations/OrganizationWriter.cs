using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Organizations;

internal sealed class OrganizationWriter(OrganizationsDbContext dbContext) : IOrganizationWriter
{
    public async Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken)
    {
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
