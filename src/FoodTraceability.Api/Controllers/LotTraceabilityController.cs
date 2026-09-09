using FoodTraceability.Api.Contracts.Traces;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Catalog.Application.Units;
using FoodTraceability.Modules.Traceability.Application.Traces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/lots/{lotId:guid}/traceability")]
public sealed class LotTraceabilityController(
    BackwardTraceQueryService queryService,
    UnitQueryService unitQueryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Returns the complete ancestry of a lot within the route organization.</summary>
    /// <remarks>
    /// The root is included even when it has no ancestors. Nodes and edges are unique.
    /// Edges point from an event's input lot to its output lot, independently of traversal direction.
    /// No partial graph is returned when Traceability:Graph:MaxNodes (default 1000) is exceeded.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="lotId">The root lot identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <response code="200">The complete graph, including the root.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide trace.read permission.</response>
    /// <response code="404">TRACEABILITY_TRACE_NOT_FOUND: the lot is missing or belongs to another organization.</response>
    /// <response code="409">TRACEABILITY_TRACE_TOO_LARGE: the graph exceeds the configured node limit.</response>
    [HttpGet("backward")]
    [Authorize(Policy = AuthorizationPolicies.TraceabilityRead)]
    [ProducesResponseType<TraceGraphResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TraceGraphResponse>> Backward(
        Guid organizationId,
        Guid lotId,
        CancellationToken cancellationToken)
    {
        TraceGraphDetails? graph;
        try
        {
            graph = await queryService.ReadAsync(organizationId, lotId, cancellationToken);
        }
        catch (TraceTooLargeException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateTraceabilityTraceTooLarge(HttpContext, exception.Message));
        }

        if (graph is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateTraceabilityTraceNotFound(HttpContext));
        }

        // Resolve foreign reference data through Catalog's existing application port in one batch.
        var unitCodes = await unitQueryService.FindCodesByIdsAsync(
            graph.Nodes.Select(node => node.UnitId).Distinct().ToArray(), cancellationToken);
        return Ok(new TraceGraphResponse(
            graph.RootLotId,
            graph.Nodes.Select(node => new TraceNodeResponse(
                node.LotId, node.LotNumber, node.ArticleId, node.Quantity, unitCodes[node.UnitId])).ToArray(),
            graph.Edges.Select(edge => new TraceEdgeResponse(
                edge.EventId, edge.EventTypeCode, edge.OccurredAt, edge.FromLotId, edge.ToLotId)).ToArray()));
    }
}
