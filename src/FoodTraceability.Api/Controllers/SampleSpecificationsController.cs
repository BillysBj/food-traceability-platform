using FoodTraceability.Api.Contracts.Specifications;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Application.Specifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/samples/{sampleId:guid}/specification")]
public sealed class SampleSpecificationsController(
    SampleSpecificationQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Gets the specification applicable to a sample at its sampling time.</summary>
    /// <remarks>
    /// Uses the same article and validity selection as PASS evaluation. Both validity boundaries
    /// are inclusive; a null validTo is unbounded. Includes required and optional parameters,
    /// ordered by parameterCode. Limits are laboratory reference values only.
    /// Only organization-wide quality.read is required. Reading does not evaluate or change the sample.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="sampleId">The sample identifier within that organization.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">The applicable specification with all parameters and reference limits.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.read permission.</response>
    /// <response code="404">QUALITY_SPECIFICATION_SAMPLE_NOT_FOUND: missing or foreign sample; QUALITY_APPLICABLE_SPECIFICATION_NOT_FOUND: visible sample without an applicable specification.</response>
    /// <response code="409">QUALITY_SPECIFICATION_AMBIGUOUS: multiple specifications apply.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.QualityRead)]
    [ProducesResponseType<SampleSpecificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SampleSpecificationResponse>> Get(
        Guid organizationId, Guid sampleId, CancellationToken cancellationToken)
    {
        SpecificationDetails? specification;
        try
        {
            specification = await queryService.FindAsync(organizationId, sampleId, cancellationToken);
        }
        catch (SpecificationSampleNotFoundException)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateQualitySpecificationSampleNotFound(HttpContext));
        }
        catch (AmbiguousSpecificationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAmbiguousSpecification(HttpContext, exception.Message));
        }

        if (specification is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateQualityApplicableSpecificationNotFound(HttpContext));
        }

        return Ok(new SampleSpecificationResponse(
            specification.Id, specification.ArticleId, specification.Version,
            specification.ValidFrom, specification.ValidTo,
            specification.Parameters.Select(parameter => new SpecificationParameterResponse(
                parameter.ParameterId, parameter.ParameterCode, parameter.StandardMethod, parameter.UnitId,
                parameter.Minimum, parameter.Maximum, parameter.Target, parameter.Required)).ToArray()));
    }
}
