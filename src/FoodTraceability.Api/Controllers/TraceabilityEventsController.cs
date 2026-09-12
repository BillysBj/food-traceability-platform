using FoodTraceability.Api.Contracts.TraceabilityEvents;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Traceability.Application.EventTypes;
using FoodTraceability.Modules.Traceability.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/traceability/events")]
public sealed class TraceabilityEventsController(
    TraceabilityEventQueryService queryService,
    CreateTraceabilityEventService createService,
    EventTypeQueryService eventTypeQueryService,
    // Cross-module reference validation belongs at the API composition root. The
    // Traceability module itself remains independent of Organizations.
    LocationQueryService locationQueryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates a traceability event in the route organization.</summary>
    /// <remarks>
    /// Input and output lines contain only a lot identifier and quantity. Each line's unit
    /// is read from its referenced lot and cannot be selected by the caller.
    /// Only event types classified as TRACEABILITY are allowed.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">The event metadata and its input and output lot lines.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created traceability event.</returns>
    /// <response code="201">Returns the newly created event.</response>
    /// <response code="400">The request or a referenced resource is invalid, a lot occurs on both sides of the event, or the event type is not allowed for traceability events.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide trace.event.create permission.</response>
    /// <response code="409">An input exceeds its lot's available quantity, or an output is an ancestor of an input and would create a cycle.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TraceabilityEventCreate)]
    [ProducesResponseType<TraceabilityEventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TraceabilityEventResponse>> Create(
        Guid organizationId,
        CreateTraceabilityEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LocationId is not Guid locationId)
        {
            return ValidationError("A location id is required.");
        }

        var location = await locationQueryService.FindByIdAsync(
            organizationId,
            locationId,
            cancellationToken);
        if (location is null)
        {
            return ValidationError(
                "The referenced location does not exist in this organization.");
        }

        if (request.OccurredAt is not DateTimeOffset occurredAt)
        {
            return ValidationError("An occurrence time is required.");
        }

        var subjectValue = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subjectValue, out var createdBy))
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAuthenticationRequired(HttpContext));
        }

        TraceabilityEventDetails traceabilityEvent;
        try
        {
            traceabilityEvent = await createService.CreateAsync(
                new CreateTraceabilityEventCommand(
                    organizationId,
                    request.EventTypeCode!,
                    locationId,
                    occurredAt,
                    request.ExternalReference,
                    request.Description,
                    createdBy,
                    MapLines(request.Inputs),
                    MapLines(request.Outputs)),
                cancellationToken);
        }
        catch (TraceabilityEventValidationException exception)
        {
            return ValidationError(exception.Message);
        }
        catch (TraceabilityEventConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateTraceabilityEventConflict(
                    HttpContext,
                    exception.Message));
        }

        var response = MapResponse(traceabilityEvent, request.EventTypeCode!.Trim().ToUpperInvariant());
        return Created(
            $"/api/v1/organizations/{organizationId}/traceability/events/{traceabilityEvent.Id}",
            response);
    }

    /// <summary>Returns one traceability event owned by the route organization.</summary>
    /// <remarks>
    /// An event owned by another organization and an event that does not exist produce the
    /// same HTTP 404 response, preventing disclosure of cross-tenant event identifiers.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="eventId">The traceability event identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested traceability event.</returns>
    /// <response code="200">Returns the requested event.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide trace.read permission.</response>
    /// <response code="404">No event exists in this organization with the supplied identifier.</response>
    [HttpGet("{eventId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TraceabilityRead)]
    [ProducesResponseType<TraceabilityEventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TraceabilityEventResponse>> GetById(
        Guid organizationId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var traceabilityEvent = await queryService.FindByIdAsync(
            organizationId,
            eventId,
            cancellationToken);
        if (traceabilityEvent is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateTraceabilityEventNotFound(HttpContext));
        }

        var eventTypeCode = await eventTypeQueryService.FindCodeByIdAsync(
            traceabilityEvent.EventTypeId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"The event type referenced by traceability event '{eventId}' does not exist.");

        return Ok(MapResponse(traceabilityEvent, eventTypeCode));
    }

    private ObjectResult ValidationError(string detail) =>
        problemDetailsFactory.CreateResult(
            problemDetailsFactory.CreateTraceabilityEventValidationError(
                HttpContext,
                detail));

    private static IReadOnlyList<TraceabilityEventLotCommand>? MapLines(
        IReadOnlyList<TraceabilityEventLotRequest>? lines) =>
        lines?
            .Select(line => line is null
                ? null!
                : new TraceabilityEventLotCommand(line.LotId, line.Quantity))
            .ToArray();

    private static TraceabilityEventResponse MapResponse(
        TraceabilityEventDetails traceabilityEvent,
        string eventTypeCode) =>
        new(
            traceabilityEvent.Id,
            traceabilityEvent.OrganizationId,
            eventTypeCode,
            traceabilityEvent.LocationId,
            traceabilityEvent.OccurredAt,
            traceabilityEvent.ExternalReference,
            traceabilityEvent.Description,
            traceabilityEvent.CreatedBy,
            traceabilityEvent.CreatedAt,
            traceabilityEvent.Inputs
                .Select(input => new TraceabilityEventLotResponse(
                    input.LotId,
                    input.Quantity,
                    input.UnitId))
                .ToArray(),
            traceabilityEvent.Outputs
                .Select(output => new TraceabilityEventLotResponse(
                    output.LotId,
                    output.Quantity,
                    output.UnitId))
                .ToArray());
}
