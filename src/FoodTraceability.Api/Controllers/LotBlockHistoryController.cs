using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots/{lotId:guid}/blocks")]
public sealed class LotBlockHistoryController(
    LotBlockQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Lists the block history of a lot within the route organization.</summary>
    /// <remarks>
    /// Ordered by BlockedAt descending, then Id descending. Page starts at 1 (default 1);
    /// pageSize is 1 to 100 (default 50). Only organization-wide quality.read is required.
    /// Open blocks have null releasedAt and releasedBy fields.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The parent lot identifier within that organization.</param>
    /// <param name="request">Page and page size.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">A page with TotalCount; an existing lot without blocks returns an empty page.</response>
    /// <response code="400">The pagination parameters are invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.read permission.</response>
    /// <response code="404">QUALITY_BLOCK_LIST_LOT_NOT_FOUND: the lot is missing or belongs to another organization.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.QualityRead)]
    [ProducesResponseType<LotBlockListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotBlockListResponse>> List(
        Guid organizationId,
        Guid lotId,
        [FromQuery] LotBlockListRequest request,
        CancellationToken cancellationToken)
    {
        var page = await queryService.ListAsync(
            new ListLotBlocksQuery(organizationId, lotId, request.Page, request.PageSize), cancellationToken);
        if (page is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateQualityBlockListLotNotFound(HttpContext));
        }

        return Ok(new LotBlockListResponse(page.Items.Select(block => new LotBlockListItemResponse(
            block.Id, block.LotId, block.Reason, block.BlockedAt, block.BlockedBy,
            block.ReleasedAt, block.ReleasedBy)).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }
}
