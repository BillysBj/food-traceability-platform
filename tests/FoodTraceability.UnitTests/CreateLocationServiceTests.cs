using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateLocationServiceTests
{
    [Fact]
    public async Task CreateUsesOrganizationScopeAndPersistsDomainLocation()
    {
        var organizationId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 2, 10, 30, 0, TimeSpan.Zero);
        var writer = new CapturingLocationWriter();
        var service = new CreateLocationService(writer, new FixedTimeProvider(now));

        var result = await service.CreateAsync(
            new CreateLocationCommand(
                organizationId,
                "  Olive Mill  ",
                "Kalamata",
                "Peloponnese",
                "gr",
                37.0389m,
                22.1142m),
            CancellationToken.None);

        var persistedLocation = Assert.IsType<Location>(writer.Location);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(result.Id, persistedLocation.Id);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(organizationId, persistedLocation.OrganizationId);
        Assert.Equal("Olive Mill", result.Name);
        Assert.Equal("GR", result.CountryCode);
        Assert.Equal(now, result.CreatedAt);
    }

    private sealed class CapturingLocationWriter : ILocationWriter
    {
        public Location? Location { get; private set; }

        public Task AddAsync(Location location, CancellationToken cancellationToken)
        {
            Location = location;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
