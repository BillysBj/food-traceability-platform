using FoodTraceability.Modules.Organizations.Application.Organizations;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Organizations;

internal sealed class OrganizationReader(OrganizationsDbContext dbContext) : IOrganizationReader
{
    public Task<OrganizationDetails?> FindByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationDetails(
                organization.Id,
                organization.Name,
                organization.VatId,
                organization.TaxNumber,
                organization.Email,
                organization.Phone,
                organization.CreatedAt,
                organization.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
