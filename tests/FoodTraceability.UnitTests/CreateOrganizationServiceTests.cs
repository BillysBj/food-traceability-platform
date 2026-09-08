using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateOrganizationServiceTests
{
    [Fact]
    public async Task ValidCommandReturnsDetailsWithNormalizedName()
    {
        var now = new DateTimeOffset(2026, 9, 8, 12, 30, 0, TimeSpan.Zero);
        var writer = new CapturingOrganizationWriter();
        var service = new CreateOrganizationService(writer, new FixedTimeProvider(now));

        var result = await service.CreateAsync(
            new CreateOrganizationCommand(
                "  Messinia Foods  ",
                "  EL123456789  ",
                "  TAX-42  ",
                "  office@example.com  ",
                "  +30 210 1234567  "),
            CancellationToken.None);

        var persistedOrganization = Assert.IsType<Organization>(writer.Organization);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(result.Id, persistedOrganization.Id);
        Assert.Equal("Messinia Foods", result.Name);
        Assert.Equal("Messinia Foods", persistedOrganization.Name);
        Assert.Equal("EL123456789", result.VatId);
        Assert.Equal("TAX-42", result.TaxNumber);
        Assert.Equal("office@example.com", result.Email);
        Assert.Equal("+30 210 1234567", result.Phone);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(now, result.UpdatedAt);
        Assert.Equal(1, writer.CallCount);
    }

    [Fact]
    public async Task MissingNameThrowsOrganizationValidationException()
    {
        var service = new CreateOrganizationService(
            new CapturingOrganizationWriter(),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var exception = await Assert.ThrowsAsync<OrganizationValidationException>(() =>
            service.CreateAsync(
                new CreateOrganizationCommand(null, null, null, null, null),
                CancellationToken.None));

        Assert.Contains("name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OptionalFieldsMayBeNull()
    {
        var writer = new CapturingOrganizationWriter();
        var service = new CreateOrganizationService(
            writer,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.CreateAsync(
            new CreateOrganizationCommand("Minimal Organization", null, null, null, null),
            CancellationToken.None);

        Assert.Null(result.VatId);
        Assert.Null(result.TaxNumber);
        Assert.Null(result.Email);
        Assert.Null(result.Phone);
        Assert.Equal(1, writer.CallCount);
    }

    [Fact]
    public async Task InvalidCommandDoesNotCallWriter()
    {
        var writer = new CapturingOrganizationWriter();
        var service = new CreateOrganizationService(
            writer,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<OrganizationValidationException>(() =>
            service.CreateAsync(
                new CreateOrganizationCommand("   ", null, null, null, null),
                CancellationToken.None));

        Assert.Equal(0, writer.CallCount);
        Assert.Null(writer.Organization);
    }

    private sealed class CapturingOrganizationWriter : IOrganizationWriter
    {
        public int CallCount { get; private set; }

        public Organization? Organization { get; private set; }

        public Task AddAsync(
            Organization organization,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Organization = organization;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
