namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class LocationQueryService(ILocationReader reader)
{
    public Task<LocationDetails?> FindByIdAsync(
        Guid organizationId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        return organizationId == Guid.Empty || locationId == Guid.Empty
            ? Task.FromResult<LocationDetails?>(null)
            : reader.FindByIdAsync(organizationId, locationId, cancellationToken);
    }

    public Task<LocationPage> ListAsync(
        ListLocationsQuery query,
        CancellationToken cancellationToken)
    {
        return query.OrganizationId == Guid.Empty
            ? Task.FromResult(new LocationPage([], query.Page, query.PageSize, 0))
            : reader.ListAsync(query, cancellationToken);
    }
}
