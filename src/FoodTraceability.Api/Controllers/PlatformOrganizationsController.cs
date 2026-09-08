using FoodTraceability.Api.Contracts.Organizations;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Organizations.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/platform/organizations")]
public sealed class PlatformOrganizationsController(
    CreateOrganizationService createService,
    OrganizationQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates an organization through the platform administration scope.</summary>
    /// <remarks>
    /// This platform endpoint has no organization context. Authorization is based exclusively
    /// on <c>organization.manage</c> from the caller's platform permissions. Organization names,
    /// VAT IDs, and tax numbers are not currently subject to uniqueness rules.
    /// </remarks>
    /// <param name="request">The organization data.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created organization.</returns>
    /// <response code="201">Returns the newly created organization.</response>
    /// <response code="400">The organization request is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide organization.manage permission.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformOrganizationManage)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationResponse>> Create(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        OrganizationDetails organization;
        try
        {
            organization = await createService.CreateAsync(
                new CreateOrganizationCommand(
                    request.Name,
                    request.VatId,
                    request.TaxNumber,
                    request.Email,
                    request.Phone),
                cancellationToken);
        }
        catch (OrganizationValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateOrganizationValidationError(
                    HttpContext,
                    exception.Message));
        }

        var response = MapResponse(organization);
        return Created(
            $"/api/v1/platform/organizations/{organization.Id}",
            response);
    }

    /// <summary>Returns an organization through the platform administration scope.</summary>
    /// <remarks>
    /// Unlike the organization-scoped GET in <c>OrganizationsController</c>, which deliberately
    /// returns HTTP 403 for an unknown organization to prevent identifier probing, this platform
    /// route returns HTTP 404. The caller is already authorized platform-wide, so disclosing that
    /// the identifier does not exist is appropriate here.
    /// </remarks>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested organization.</returns>
    /// <response code="200">Returns the requested organization.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide organization.manage permission.</response>
    /// <response code="404">No organization exists with the supplied identifier.</response>
    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformOrganizationManage)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationResponse>> GetById(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await queryService.FindByIdAsync(
            organizationId,
            cancellationToken);
        if (organization is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateOrganizationNotFound(HttpContext));
        }

        return Ok(MapResponse(organization));
    }

    private static OrganizationResponse MapResponse(OrganizationDetails organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.VatId,
            organization.TaxNumber,
            organization.Email,
            organization.Phone,
            organization.CreatedAt,
            organization.UpdatedAt);
}
