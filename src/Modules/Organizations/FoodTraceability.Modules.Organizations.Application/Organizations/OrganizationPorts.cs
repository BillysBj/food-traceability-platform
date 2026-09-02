namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public interface IOrganizationReader
{
    Task<OrganizationDetails?> FindByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}

public interface ILocationWriter
{
    Task AddAsync(
        FoodTraceability.Modules.Organizations.Domain.Location location,
        CancellationToken cancellationToken);
}
