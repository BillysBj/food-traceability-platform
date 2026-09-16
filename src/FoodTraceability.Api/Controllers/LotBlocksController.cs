using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Platform.Contracts.Traceability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots/{lotId:guid}/blocks")]
public sealed class LotBlocksController(
    BlockLotService blockService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Blocks a lot and records the reason, time and decision maker atomically.</summary>
    /// <remarks>
    /// Requires only organization-wide quality.block. A foreign lot and an unknown lot
    /// return the same 404 response. The Location identifies the block; retrieval is not implemented.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The lot identifier within that organization.</param>
    /// <param name="request">The reason for blocking the lot.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="201">The created open block and resulting BLOCKED lot status.</response>
    /// <response code="400">LOT_BLOCK_VALIDATION_FAILED: invalid request.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.block permission.</response>
    /// <response code="404">LOT_BLOCK_LOT_NOT_FOUND: the lot does not exist in this organization.</response>
    /// <response code="409">LOT_BLOCK_CONFLICT: the lot already has an open block.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.QualityBlock)]
    [ProducesResponseType<LotBlockResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotBlockResponse>> Create(
        Guid organizationId,
        Guid lotId,
        BlockLotRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var blockedBy))
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAuthenticationRequired(HttpContext));
        }

        LotBlockDetails block;
        try
        {
            block = await blockService.BlockAsync(
                new BlockLotCommand(organizationId, lotId, request.Reason, blockedBy), cancellationToken);
        }
        catch (LotBlockValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotBlockValidationError(HttpContext, exception.Message));
        }
        catch (LotBlockNotFoundException)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotBlockNotFound(HttpContext));
        }
        catch (LotBlockConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotBlockConflict(HttpContext, exception.Message));
        }

        var response = new LotBlockResponse(block.Id, block.LotId, block.Reason,
            block.BlockedAt, block.BlockedBy, block.QualityStatus switch
            {
                LotQualityStatus.Blocked => "BLOCKED",
                _ => throw new InvalidOperationException($"Unexpected block result status '{block.QualityStatus}'."),
            });
        return Created($"/api/v1/organizations/{organizationId}/lots/{lotId}/blocks/{block.Id}", response);
    }
}
