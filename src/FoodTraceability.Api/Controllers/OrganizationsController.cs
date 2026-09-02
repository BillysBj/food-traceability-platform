using FoodTraceability.Api.Contracts.Organizations;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Organizations.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations")]
public sealed class OrganizationsController(
    OrganizationQueryService queryService,
    CreateLocationService createLocationService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates a location in the organization selected by the route.</summary>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">The location data. It cannot select an organization.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created location.</returns>
    [HttpPost("{organizationId:guid}/locations")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationManage)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LocationResponse>> CreateLocation(
        Guid organizationId,
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await createLocationService.CreateAsync(
            new CreateLocationCommand(
                organizationId,
                request.Name,
                request.City,
                request.Region,
                request.CountryCode,
                request.Latitude,
                request.Longitude),
            cancellationToken);
        var response = new LocationResponse(
            location.Id,
            location.OrganizationId,
            location.Name,
            location.City,
            location.Region,
            location.CountryCode,
            location.Latitude,
            location.Longitude,
            location.CreatedAt);

        return Created(
            $"/api/v1/organizations/{organizationId}/locations/{location.Id}",
            response);
    }

    /// <summary>Returns an organization visible in the caller's organization-wide scope.</summary>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested organization.</returns>
    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationRead)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationResponse>> GetById(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await queryService.FindByIdAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAuthorizationDenied(HttpContext));
        }

        return Ok(new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.VatId,
            organization.TaxNumber,
            organization.Email,
            organization.Phone,
            organization.CreatedAt,
            organization.UpdatedAt));
    }
}
