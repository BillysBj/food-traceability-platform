using FoodTraceability.Modules.Organizations.Application.Organizations;

namespace FoodTraceability.UnitTests;

public sealed class LocationQueryServiceTests
{
    [Fact]
    public async Task EmptyOrganizationOrLocationReturnsNullWithoutCallingReader()
    {
        var reader = new CapturingLocationReader();
        var service = new LocationQueryService(reader);

        var emptyOrganizationResult = await service.FindByIdAsync(
            Guid.Empty,
            Guid.NewGuid(),
            CancellationToken.None);
        var emptyLocationResult = await service.FindByIdAsync(
            Guid.NewGuid(),
            Guid.Empty,
            CancellationToken.None);

        Assert.Null(emptyOrganizationResult);
        Assert.Null(emptyLocationResult);
        Assert.Equal(0, reader.FindByIdCallCount);
    }

    [Fact]
    public async Task EmptyOrganizationReturnsEmptyPageWithoutCallingReader()
    {
        var reader = new CapturingLocationReader();
        var service = new LocationQueryService(reader);

        var result = await service.ListAsync(
            new ListLocationsQuery(Guid.Empty, 3, 25),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Null(reader.ListQuery);
    }

    [Fact]
    public async Task PageAndPageSizeReachReaderUnchanged()
    {
        var reader = new CapturingLocationReader();
        var service = new LocationQueryService(reader);

        await service.ListAsync(
            new ListLocationsQuery(Guid.NewGuid(), 7, 83),
            CancellationToken.None);

        Assert.Equal(7, reader.ListQuery?.Page);
        Assert.Equal(83, reader.ListQuery?.PageSize);
    }

    private sealed class CapturingLocationReader : ILocationReader
    {
        public int FindByIdCallCount { get; private set; }

        public ListLocationsQuery? ListQuery { get; private set; }

        public Task<LocationDetails?> FindByIdAsync(
            Guid organizationId,
            Guid locationId,
            CancellationToken cancellationToken)
        {
            FindByIdCallCount++;
            return Task.FromResult<LocationDetails?>(null);
        }

        public Task<LocationPage> ListAsync(
            ListLocationsQuery query,
            CancellationToken cancellationToken)
        {
            ListQuery = query;
            return Task.FromResult(new LocationPage([], query.Page, query.PageSize, 0));
        }
    }
}
