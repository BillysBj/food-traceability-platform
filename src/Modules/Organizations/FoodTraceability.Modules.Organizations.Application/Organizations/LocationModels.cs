namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed record CreateLocationCommand(
    Guid OrganizationId,
    string? Name,
    string? City,
    string? Region,
    string? CountryCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record LocationDetails(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? City,
    string? Region,
    string? CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset CreatedAt);
