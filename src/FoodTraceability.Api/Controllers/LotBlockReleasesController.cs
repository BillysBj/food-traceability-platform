using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Platform.Contracts.Traceability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots/{lotId:guid}/blocks/{blockId:guid}/release")]
public sealed class LotBlockReleasesController(
    ReleaseLotBlockService releaseService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Releases exactly the identified block and sets the lot quality status atomically.</summary>
    /// <remarks>
    /// Requires only organization-wide quality.release. No request body is required.
    /// The block identifier prevents an old request from releasing a newer block of the same lot.
    /// Returns 200 because an existing block is updated, not created.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The lot identifier within that organization.</param>
    /// <param name="blockId">The exact block to release.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">The released block and resulting RELEASED lot status.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.release permission.</response>
    /// <response code="404">LOT_BLOCK_RELEASE_NOT_FOUND: no block matches this organization, lot and block identifier.</response>
    /// <response code="409">LOT_BLOCK_RELEASE_CONFLICT: the block was already released, including by a concurrent request.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.QualityRelease)]
    [ProducesResponseType<ReleasedLotBlockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReleasedLotBlockResponse>> Release(
        Guid organizationId, Guid lotId, Guid blockId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var releasedBy) || releasedBy == Guid.Empty)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAuthenticationRequired(HttpContext));
        }

        ReleasedLotBlockDetails block;
        try
        {
            block = await releaseService.ReleaseAsync(
                new ReleaseLotBlockCommand(organizationId, lotId, blockId, releasedBy), cancellationToken);
        }
        catch (ReleaseLotBlockNotFoundException)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotBlockReleaseNotFound(HttpContext));
        }
        catch (LotBlockAlreadyReleasedException)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotBlockReleaseConflict(HttpContext));
        }

        return Ok(new ReleasedLotBlockResponse(block.Id, block.LotId, block.Reason,
            block.BlockedAt, block.BlockedBy, block.ReleasedAt, block.ReleasedBy, block.QualityStatus switch
            {
                LotQualityStatus.Released => "RELEASED",
                _ => throw new InvalidOperationException($"Unexpected release result status '{block.QualityStatus}'."),
            }));
    }
}
