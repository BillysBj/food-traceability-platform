using FoodTraceability.Api.Contracts.LabResults;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.LabResults;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/samples/{sampleId:guid}/results")]
public sealed class LabResultsController(
    CreateLabResultService createService,
    LabResultQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Lists laboratory results of a sample within the route organization.</summary>
    /// <remarks>
    /// Ordered by CreatedAt descending, then Id descending. Page starts at 1 (default 1);
    /// pageSize is 1 to 100 (default 50). Only organization-wide quality.read is required.
    /// Assessments are the stored PASS or FAIL codes.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="sampleId">The parent sample identifier within that organization.</param>
    /// <param name="request">Page and page size.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">A page with TotalCount; an existing sample without results returns an empty page.</response>
    /// <response code="400">The pagination parameters are invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.read permission.</response>
    /// <response code="404">QUALITY_RESULT_LIST_SAMPLE_NOT_FOUND: the sample is missing or belongs to another organization.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.QualityRead)]
    [ProducesResponseType<LabResultListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabResultListResponse>> List(
        Guid organizationId,
        Guid sampleId,
        [FromQuery] LabResultListRequest request,
        CancellationToken cancellationToken)
    {
        var page = await queryService.ListAsync(
            new ListLabResultsQuery(organizationId, sampleId, request.Page, request.PageSize), cancellationToken);
        if (page is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateQualityResultListSampleNotFound(HttpContext));
        }

        return Ok(new LabResultListResponse(page.Items.Select(result => new LabResultListItemResponse(
            result.Id, result.SampleId, result.ParameterId, result.Value,
            result.Assessment switch
            {
                LabResultAssessment.Pass => "PASS",
                LabResultAssessment.Fail => "FAIL",
                _ => throw new InvalidOperationException($"Unknown lab result assessment '{result.Assessment}'."),
            },
            result.Method, result.MeasuredAt, result.CreatedAt)).ToArray(),
            page.Page, page.PageSize, page.TotalCount));
    }

    /// <summary>Records one laboratory result for a sample and parameter.</summary>
    /// <remarks>
    /// FAIL sets the sample to FAIL permanently. PASS completes a PENDING sample when all
    /// required parameters of its applicable article specification have PASS results (D-52).
    /// Without an applicable specification the sample remains PENDING. Limits are reference values only.
    /// Only organization-wide quality.result.create is required. Result and sample change
    /// are saved atomically. The Location identifies the result; individual retrieval is not yet implemented.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="sampleId">The sample identifier within that organization.</param>
    /// <param name="request">Parameter, measurement, assessment, method and measurement time.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="201">The created result and resulting sample status.</response>
    /// <response code="400">LAB_RESULT_VALIDATION_FAILED: invalid request or unknown parameter.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.result.create permission.</response>
    /// <response code="404">LAB_RESULT_SAMPLE_NOT_FOUND: the sample does not exist in this organization.</response>
    /// <response code="409">LAB_RESULT_CONFLICT: duplicate parameter result; QUALITY_SPECIFICATION_AMBIGUOUS: multiple specifications apply.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.LabResultCreate)]
    [ProducesResponseType<LabResultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LabResultResponse>> Create(
        Guid organizationId,
        Guid sampleId,
        CreateLabResultRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MeasuredAt is not DateTimeOffset measuredAt)
        {
            return ValidationError("A measurement time is required.");
        }

        if (request.Value is not decimal value)
        {
            return ValidationError("A measurement value is required.");
        }

        LabResultAssessment? assessment = request.Assessment switch
        {
            "PASS" => LabResultAssessment.Pass,
            "FAIL" => LabResultAssessment.Fail,
            _ => null,
        };
        if (assessment is null)
        {
            return ValidationError("Lab result assessment must be PASS or FAIL.");
        }

        LabResultDetails result;
        try
        {
            result = await createService.CreateAsync(
                new CreateLabResultCommand(organizationId, sampleId, request.ParameterId,
                    value, assessment.Value, request.Method, measuredAt),
                cancellationToken);
        }
        catch (LabResultSampleNotFoundException)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLabResultSampleNotFound(HttpContext));
        }
        catch (LabResultValidationException exception)
        {
            return ValidationError(exception.Message);
        }
        catch (LabResultConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLabResultConflict(HttpContext, exception.Message));
        }
        catch (AmbiguousSpecificationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAmbiguousSpecification(HttpContext, exception.Message));
        }

        var response = new LabResultResponse(
            result.Id, result.SampleId, result.ParameterId, result.Value,
            result.Assessment switch
            {
                LabResultAssessment.Pass => "PASS",
                LabResultAssessment.Fail => "FAIL",
                _ => throw new InvalidOperationException($"Unknown lab result assessment '{result.Assessment}'."),
            },
            result.Method, result.MeasuredAt,
            result.SampleStatus switch
            {
                SampleStatus.Pending => "PENDING",
                SampleStatus.Pass => "PASS",
                SampleStatus.Fail => "FAIL",
                _ => throw new InvalidOperationException($"Unknown sample status '{result.SampleStatus}'."),
            });
        return Created($"/api/v1/organizations/{organizationId}/samples/{sampleId}/results/{result.Id}", response);
    }

    private ObjectResult ValidationError(string detail) =>
        problemDetailsFactory.CreateResult(
            problemDetailsFactory.CreateLabResultValidationError(HttpContext, detail));
}
