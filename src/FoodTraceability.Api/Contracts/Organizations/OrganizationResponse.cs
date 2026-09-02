namespace FoodTraceability.Api.Contracts.Organizations;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string? VatId,
    string? TaxNumber,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
