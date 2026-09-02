using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class CreateLocationService(
    ILocationWriter writer,
    TimeProvider timeProvider)
{
    public async Task<LocationDetails> CreateAsync(
        CreateLocationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var countryCode = command.CountryCode is null
            ? null
            : CountryCode.Create(command.CountryCode);
        var location = Location.Create(
            Guid.NewGuid(),
            command.OrganizationId,
            command.Name,
            command.City,
            command.Region,
            countryCode,
            command.Latitude,
            command.Longitude,
            timeProvider.GetUtcNow());

        await writer.AddAsync(location, cancellationToken);

        return new LocationDetails(
            location.Id,
            location.OrganizationId,
            location.Name,
            location.City,
            location.Region,
            location.CountryCode?.Value,
            location.Latitude,
            location.Longitude,
            location.CreatedAt);
    }
}
