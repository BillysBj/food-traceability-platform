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
    OrganizationQueryService organizationQueryService,
    LocationQueryService locationQueryService,
    CreateLocationService createLocationService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Returns a page of locations owned by the organization selected by the route.</summary>
    /// <remarks>
    /// Results always use the fixed order <c>createdAt DESC</c>, followed by
    /// <c>locationId DESC</c>. The location identifier is the mandatory tie-breaker that keeps
    /// offset pages deterministic when multiple locations have the same creation timestamp.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">Pagination values.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>A page of locations and the total number of locations in the organization.</returns>
    /// <response code="200">Returns the requested page, including an empty page when applicable.</response>
    /// <response code="400">One or more query parameters are invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide organization.read permission.</response>
    [HttpGet("{organizationId:guid}/locations")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationRead)]
    [ProducesResponseType<LocationListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LocationListResponse>> ListLocations(
        Guid organizationId,
        [FromQuery] LocationListRequest request,
        CancellationToken cancellationToken)
    {
        var page = await locationQueryService.ListAsync(
            new ListLocationsQuery(organizationId, request.Page, request.PageSize),
            cancellationToken);
        var items = page.Items
            .Select(MapLocationResponse)
            .ToArray();

        return Ok(new LocationListResponse(items, page.Page, page.PageSize, page.TotalCount));
    }

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

    /// <summary>Returns a location owned by the organization selected by the route.</summary>
    /// <remarks>
    /// A location owned by another organization and a location that does not exist both produce
    /// the same HTTP 404 Not Found response. This prevents disclosure of cross-tenant location IDs.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="locationId">The location identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested location.</returns>
    /// <response code="200">Returns the requested location.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide organization.read permission.</response>
    /// <response code="404">No location exists in this organization with the supplied identifier.</response>
    [HttpGet("{organizationId:guid}/locations/{locationId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OrganizationRead)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationResponse>> GetLocationById(
        Guid organizationId,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        var location = await locationQueryService.FindByIdAsync(
            organizationId,
            locationId,
            cancellationToken);
        if (location is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLocationNotFound(HttpContext));
        }

        return Ok(MapLocationResponse(location));
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
        var organization = await organizationQueryService.FindByIdAsync(
            organizationId,
            cancellationToken);
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

    private static LocationResponse MapLocationResponse(LocationDetails location) =>
        new(
            location.Id,
            location.OrganizationId,
            location.Name,
            location.City,
            location.Region,
            location.CountryCode,
            location.Latitude,
            location.Longitude,
            location.CreatedAt);
}
