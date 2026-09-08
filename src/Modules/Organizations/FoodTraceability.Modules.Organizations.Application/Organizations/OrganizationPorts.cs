using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public interface IOrganizationReader
{
    Task<OrganizationDetails?> FindByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}

public interface IOrganizationWriter
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken);
}

public interface ILocationWriter
{
    Task AddAsync(
        Location location,
        CancellationToken cancellationToken);
}
