namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public interface IOrganizationReader
{
    Task<OrganizationDetails?> FindByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}
