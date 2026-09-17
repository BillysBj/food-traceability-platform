using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.Samples;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots/{lotId:guid}/samples")]
public sealed class LotSamplesController(
    SampleQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Lists the stored samples of a lot within the route organization.</summary>
    /// <remarks>
    /// Ordered by CreatedAt descending, then Id descending. Page starts at 1 (default 1);
    /// pageSize is 1 to 100 (default 50). Only organization-wide quality.read is required.
    /// Status is the stored PENDING, PASS or FAIL code; reading does not evaluate results.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The parent lot identifier within that organization.</param>
    /// <param name="request">Page and page size.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">A page with TotalCount; an existing lot without samples returns an empty page.</response>
    /// <response code="400">The pagination parameters are invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.read permission.</response>
    /// <response code="404">QUALITY_SAMPLE_LIST_LOT_NOT_FOUND: the lot is missing or belongs to another organization.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.QualityRead)]
    [ProducesResponseType<SampleListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SampleListResponse>> List(
        Guid organizationId,
        Guid lotId,
        [FromQuery] SampleListRequest request,
        CancellationToken cancellationToken)
    {
        var page = await queryService.ListAsync(
            new ListSamplesQuery(organizationId, lotId, request.Page, request.PageSize), cancellationToken);
        if (page is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateQualitySampleListLotNotFound(HttpContext));
        }

        return Ok(new SampleListResponse(page.Items.Select(sample => new SampleListItemResponse(
            sample.Id, sample.SampleNumber,
            sample.Status switch
            {
                SampleStatus.Pending => "PENDING",
                SampleStatus.Pass => "PASS",
                SampleStatus.Fail => "FAIL",
                _ => throw new InvalidOperationException($"Unknown sample status '{sample.Status}'."),
            },
            sample.TakenAt, sample.LotId, sample.LocationId, sample.TraceabilityEventId,
            sample.CreatedAt)).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
}
