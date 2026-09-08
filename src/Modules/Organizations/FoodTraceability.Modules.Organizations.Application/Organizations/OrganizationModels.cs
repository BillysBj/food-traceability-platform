namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed record CreateOrganizationCommand(
    string? Name,
    string? VatId,
    string? TaxNumber,
    string? Email,
    string? Phone);

public sealed record OrganizationDetails(
    Guid Id,
    string Name,
    string? VatId,
    string? TaxNumber,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
