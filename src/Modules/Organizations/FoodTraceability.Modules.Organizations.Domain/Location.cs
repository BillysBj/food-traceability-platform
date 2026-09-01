namespace FoodTraceability.Modules.Organizations.Domain;

public sealed class Location
{
    public const int MaximumNameLength = 200;
    public const int MaximumCityLength = 100;
    public const int MaximumRegionLength = 100;

    private Location(
        Guid id,
        Guid organizationId,
        string name,
        string? city,
        string? region,
        CountryCode? countryCode,
        decimal? latitude,
        decimal? longitude,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        City = city;
        Region = region;
        CountryCode = countryCode;
        Latitude = latitude;
        Longitude = longitude;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public string Name { get; }

    public string? City { get; }

    public string? Region { get; }

    public CountryCode? CountryCode { get; }

    public decimal? Latitude { get; }

    public decimal? Longitude { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Location Create(
        Guid id,
        Guid organizationId,
        string? name,
        string? city,
        string? region,
        CountryCode? countryCode,
        decimal? latitude,
        decimal? longitude,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new OrganizationsDomainException("Location id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new OrganizationsDomainException("Location organization id must not be empty.");
        }

        ValidateCoordinates(latitude, longitude);

        return new Location(
            id,
            organizationId,
            NormalizeRequired(name, "Location name", MaximumNameLength),
            NormalizeOptional(city, "City", MaximumCityLength),
            NormalizeOptional(region, "Region", MaximumRegionLength),
            countryCode,
            latitude,
            longitude,
            createdAt);
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new OrganizationsDomainException(
                "Latitude and longitude must either both be provided or both be absent.");
        }

        if (latitude is < -90 or > 90)
        {
            throw new OrganizationsDomainException("Latitude must be between -90 and 90 inclusive.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new OrganizationsDomainException("Longitude must be between -180 and 180 inclusive.");
        }
    }

    private static string NormalizeRequired(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not be null, empty, or consist only of whitespace.");
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private static string? NormalizeOptional(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new OrganizationsDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
