using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.UnitTests;

public sealed class LocationTests
{
    private static readonly Guid LocationId =
        Guid.Parse("7c94d426-408d-4ad6-a4d2-50299c173007");
    private static readonly Guid OrganizationId =
        Guid.Parse("ba1c29c5-b4be-4e51-bfa4-beb46b934673");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object?[]> OutOfRangeCoordinates()
    {
        yield return [-90.000001m, 0m];
        yield return [90.000001m, 0m];
        yield return [0m, -180.000001m];
        yield return [0m, 180.000001m];
    }

    public static IEnumerable<object?[]> PartialCoordinates()
    {
        yield return [42m, null];
        yield return [null, 24m];
    }

    [Fact]
    public void ValidLocationIsCreatedWithExpectedValues()
    {
        var countryCode = CountryCode.Create("gr");

        var location = Location.Create(
            LocationId,
            OrganizationId,
            "  Athens Warehouse  ",
            "  Athens  ",
            "  Attica  ",
            countryCode,
            37.983810m,
            23.727539m,
            CreatedAt);

        Assert.Equal(LocationId, location.Id);
        Assert.Equal(OrganizationId, location.OrganizationId);
        Assert.Equal("Athens Warehouse", location.Name);
        Assert.Equal("Athens", location.City);
        Assert.Equal("Attica", location.Region);
        Assert.Equal(countryCode, location.CountryCode);
        Assert.Equal(37.983810m, location.Latitude);
        Assert.Equal(23.727539m, location.Longitude);
        Assert.Equal(CreatedAt, location.CreatedAt);
    }

    [Fact]
    public void EmptyOrganizationIdOnLocationIsRejected()
    {
        Assert.Throws<OrganizationsDomainException>(() => Location.Create(
            LocationId,
            Guid.Empty,
            "Athens Warehouse",
            null,
            null,
            null,
            null,
            null,
            CreatedAt));
    }

    [Theory]
    [MemberData(nameof(OutOfRangeCoordinates))]
    public void CoordinatesOutOfRangeAreRejected(decimal latitude, decimal longitude)
    {
        Assert.Throws<OrganizationsDomainException>(() => Location.Create(
            LocationId,
            OrganizationId,
            "Athens Warehouse",
            null,
            null,
            null,
            latitude,
            longitude,
            CreatedAt));
    }

    [Theory]
    [MemberData(nameof(PartialCoordinates))]
    public void PartialCoordinatesAreRejected(decimal? latitude, decimal? longitude)
    {
        Assert.Throws<OrganizationsDomainException>(() => Location.Create(
            LocationId,
            OrganizationId,
            "Athens Warehouse",
            null,
            null,
            null,
            latitude,
            longitude,
            CreatedAt));
    }
}
