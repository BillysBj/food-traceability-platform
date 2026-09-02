using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Api.Contracts.Organizations;

public sealed record CreateLocationRequest(
    [Required, StringLength(Location.MaximumNameLength)] string? Name,
    [StringLength(Location.MaximumCityLength)] string? City,
    [StringLength(Location.MaximumRegionLength)] string? Region,
    [RegularExpression(@"^\s*[A-Za-z]{2}\s*$")] string? CountryCode,
    [Range(-90, 90)] decimal? Latitude,
    [Range(-180, 180)] decimal? Longitude) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult(
                "Latitude and longitude must either both be provided or both be absent.",
                [nameof(Latitude), nameof(Longitude)]);
        }
    }
}
