namespace FoodTraceability.Api.Contracts.Organizations;

public sealed record LocationResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? City,
    string? Region,
    string? CountryCode,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset CreatedAt);
