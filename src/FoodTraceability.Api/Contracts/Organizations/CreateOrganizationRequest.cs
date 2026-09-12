using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Api.Contracts.Organizations;

public sealed record CreateOrganizationRequest(
    [Required, StringLength(Organization.MaximumNameLength)] string? Name,
    string? VatId,
    [StringLength(Organization.MaximumTaxNumberLength)] string? TaxNumber,
    [StringLength(Organization.MaximumEmailLength)] string? Email,
    [StringLength(Organization.MaximumPhoneLength)] string? Phone);
