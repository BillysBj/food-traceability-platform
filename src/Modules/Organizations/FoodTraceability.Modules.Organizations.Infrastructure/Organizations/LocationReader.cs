using FoodTraceability.Modules.Organizations.Application.Organizations;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Organizations;

internal sealed class LocationReader(OrganizationsDbContext dbContext) : ILocationReader
{
    public Task<LocationDetails?> FindByIdAsync(
        Guid organizationId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Locations
            .AsNoTracking()
            .Where(location => location.Id == locationId
                && location.OrganizationId == organizationId)
            .Select(location => new LocationDetails(
                location.Id,
                location.OrganizationId,
                location.Name,
                location.City,
                location.Region,
                location.CountryCode == null ? null : location.CountryCode.Value,
                location.Latitude,
                location.Longitude,
                location.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<LocationPage> ListAsync(
        ListLocationsQuery query,
        CancellationToken cancellationToken)
    {
        var locations = dbContext.Locations
            .AsNoTracking()
            .Where(location => location.OrganizationId == query.OrganizationId);

        var totalCount = await locations.LongCountAsync(cancellationToken);
        var items = await locations
            .OrderByDescending(location => location.CreatedAt)
            .ThenByDescending(location => location.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(location => new LocationDetails(
                location.Id,
                location.OrganizationId,
                location.Name,
                location.City,
                location.Region,
                location.CountryCode == null ? null : location.CountryCode.Value,
                location.Latitude,
                location.Longitude,
                location.CreatedAt))
            .ToListAsync(cancellationToken);

        return new LocationPage(items, query.Page, query.PageSize, totalCount);
    }
}
