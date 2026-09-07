using FoodTraceability.Api.Contracts.Lots;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Catalog.Application.Units;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Traceability.Application.Lots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots")]
public sealed class LotsController(
    LotQueryService queryService,
    CreateLotService createService,
    // Cross-module code/id composition belongs at the API composition root. The
    // Traceability module itself remains independent of Catalog.
    UnitQueryService unitQueryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Returns a page of lots owned by the organization selected by the route.</summary>
    /// <remarks>
    /// Results always use the fixed order <c>createdAt DESC</c>, followed by
    /// <c>lotId DESC</c>. The lot identifier is the mandatory tie-breaker that keeps offset
    /// pages deterministic when multiple lots have the same creation timestamp.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">Pagination values and optional exact-match filters.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>A page of lots and the total number of matching lots.</returns>
    /// <response code="200">Returns the requested page, including an empty page when applicable.</response>
    /// <response code="400">One or more query parameters are invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide lot.read permission.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.LotRead)]
    [ProducesResponseType<LotListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LotListResponse>> List(
        Guid organizationId,
        [FromQuery] LotListRequest request,
        CancellationToken cancellationToken)
    {
        var page = await queryService.ListAsync(
            new ListLotsQuery(
                organizationId,
                request.Page,
                request.PageSize,
                request.ArticleId,
                request.LotNumber),
            cancellationToken);
        var unitIds = page.Items
            .Select(lot => lot.UnitId)
            .Distinct()
            .ToArray();
        var unitCodes = await unitQueryService.FindCodesByIdsAsync(
            unitIds,
            cancellationToken);
        var items = page.Items
            .Select(lot => unitCodes.TryGetValue(lot.UnitId, out var unitCode)
                ? MapResponse(lot, unitCode)
                : throw new InvalidOperationException(
                    $"The unit referenced by lot '{lot.Id}' does not exist."))
            .ToArray();

        return Ok(new LotListResponse(items, page.Page, page.PageSize, page.TotalCount));
    }

    /// <summary>Creates a lot in the organization selected by the route.</summary>
    /// <remarks>
    /// The request body cannot select the organization. An <c>articleId</c> that does not
    /// identify an article in the route organization produces HTTP 400 Bad Request, not HTTP
    /// 404 Not Found: it is a referenced input of the create command, and missing and
    /// cross-organization article references are intentionally indistinguishable.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">The lot data, including its article and unit references.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created lot.</returns>
    /// <response code="201">Returns the newly created lot.</response>
    /// <response code="400">The request, referenced article, or referenced unit is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide lot.create permission.</response>
    /// <response code="409">The lot number is already assigned in the organization.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.LotCreate)]
    [ProducesResponseType<LotResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotResponse>> Create(
        Guid organizationId,
        CreateLotRequest request,
        CancellationToken cancellationToken)
    {
        var unitId = await unitQueryService.FindIdByCodeAsync(
            request.UnitCode!,
            cancellationToken);
        if (unitId is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotValidationError(
                    HttpContext,
                    "The referenced unit does not exist."));
        }

        LotDetails lot;
        try
        {
            lot = await createService.CreateAsync(
                new CreateLotCommand(
                    organizationId,
                    request.ArticleId,
                    request.LotNumber,
                    request.Quantity,
                    unitId.Value),
                cancellationToken);
        }
        catch (LotValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (LotConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotConflict(HttpContext, exception.Message));
        }

        var normalizedUnitCode = UnitCode.Create(request.UnitCode!).Value;
        var response = MapResponse(lot, normalizedUnitCode);
        return Created(
            $"/api/v1/organizations/{organizationId}/lots/{lot.Id}",
            response);
    }

    /// <summary>Returns a lot owned by the organization selected by the route.</summary>
    /// <remarks>
    /// A lot owned by another organization and a lot that does not exist both produce the
    /// same HTTP 404 Not Found response. This prevents disclosure of cross-tenant lot IDs.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The lot identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested lot.</returns>
    /// <response code="200">Returns the requested lot.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide lot.read permission.</response>
    /// <response code="404">No lot exists in this organization with the supplied identifier.</response>
    [HttpGet("{lotId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.LotRead)]
    [ProducesResponseType<LotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotResponse>> GetById(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        var lot = await queryService.FindByIdAsync(
            organizationId,
            lotId,
            cancellationToken);
        if (lot is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateLotNotFound(HttpContext));
        }

        var unitCode = await unitQueryService.FindCodeByIdAsync(
            lot.UnitId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"The unit referenced by lot '{lot.Id}' does not exist.");

        return Ok(MapResponse(lot, unitCode));
    }

    private static LotResponse MapResponse(LotDetails lot, string unitCode) =>
        new(
            lot.Id,
            lot.OrganizationId,
            lot.ArticleId,
            lot.LotNumber,
            lot.Quantity,
            unitCode,
            lot.CreatedAt);
}
