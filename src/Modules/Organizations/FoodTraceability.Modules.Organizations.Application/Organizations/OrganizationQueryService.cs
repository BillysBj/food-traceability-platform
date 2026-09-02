namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class OrganizationQueryService(IOrganizationReader reader)
{
    public Task<OrganizationDetails?> FindByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return Task.FromResult<OrganizationDetails?>(null);
        }

        return reader.FindByIdAsync(organizationId, cancellationToken);
    }
}
