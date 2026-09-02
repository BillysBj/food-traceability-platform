using FoodTraceability.Modules.Organizations.Application.Organizations;

namespace FoodTraceability.UnitTests;

public sealed class OrganizationQueryServiceTests
{
    [Fact]
    public async Task EmptyOrganizationIdDoesNotReachReader()
    {
        var reader = new StubOrganizationReader();
        var service = new OrganizationQueryService(reader);

        var result = await service.FindByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, reader.CallCount);
    }

    private sealed class StubOrganizationReader : IOrganizationReader
    {
        public int CallCount { get; private set; }

        public Task<OrganizationDetails?> FindByIdAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<OrganizationDetails?>(null);
        }
    }
}
