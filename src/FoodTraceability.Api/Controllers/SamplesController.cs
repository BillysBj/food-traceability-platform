using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Quality.Application.Samples;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/samples")]
public sealed class SamplesController(
    CreateSampleService createService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates a pending sample and books its withdrawn quantity atomically.</summary>
    /// <remarks>
    /// Creates one SAMPLE event with one input and no outputs. The quantity uses the
    /// referenced lot's unit. Sample numbers are unique per organization ignoring case.
    /// Only quality.sample.create is required; no additional trace.event.create permission.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">The sample number, lot, location, quantity and sampling time.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created sample and its traceability event identifier.</returns>
    /// <response code="201">Returns the pending sample and its resource location.</response>
    /// <response code="400">The sample request is invalid or a referenced resource does not belong to this organization.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide quality.sample.create permission.</response>
    /// <response code="409">The sample number already exists or the withdrawn quantity exceeds availability.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SampleCreate)]
    [ProducesResponseType<SampleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SampleResponse>> Create(
        Guid organizationId,
        CreateSampleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TakenAt is not DateTimeOffset takenAt)
        {
            return ValidationError("A sampling time is required.");
        }

        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var createdBy))
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateAuthenticationRequired(HttpContext));
        }

        SampleDetails sample;
        try
        {
            sample = await createService.CreateAsync(
                new CreateSampleCommand(
                    organizationId,
                    request.SampleNumber,
                    request.LotId,
                    request.LocationId,
                    request.Quantity,
                    takenAt,
                    createdBy),
                cancellationToken);
        }
        catch (SampleValidationException exception)
        {
            return ValidationError(exception.Message);
        }
        catch (SampleConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateSampleConflict(HttpContext, exception.Message));
        }

        var response = new SampleResponse(
            sample.Id,
            sample.SampleNumber,
            sample.Status switch
            {
                SampleStatus.Pending => "PENDING",
                SampleStatus.Pass => "PASS",
                SampleStatus.Fail => "FAIL",
                _ => throw new InvalidOperationException($"Unknown sample status '{sample.Status}'."),
            },
            sample.TakenAt,
            sample.LotId,
            sample.LocationId,
            sample.TraceabilityEventId);
        return Created($"/api/v1/organizations/{organizationId}/samples/{sample.Id}", response);
    }

    private ObjectResult ValidationError(string detail) =>
        problemDetailsFactory.CreateResult(
            problemDetailsFactory.CreateSampleValidationError(HttpContext, detail));
}
