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
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
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
