using FoodTraceability.Api.Contracts.Users;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Identity.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/platform/users")]
public sealed class PlatformUsersController(
    CreateUserService createService,
    UserQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates a global user through the platform administration scope.</summary>
    /// <remarks>
    /// Both platform-user endpoints are authorized exclusively through platform permissions.
    /// An organization-scoped assignment of <c>user.manage</c> or <c>user.read</c> is not
    /// sufficient. See decisions D-27 and D-37. The initial password is accepted only as a
    /// credential and is never returned.
    /// </remarks>
    /// <param name="request">The global user data and initial password.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created user without credential data.</returns>
    /// <response code="201">Returns the newly created user.</response>
    /// <response code="400">The user request is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide user.manage permission.</response>
    /// <response code="409">A user with the same global email address already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformUserManage)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        UserDetails user;
        try
        {
            user = await createService.CreateAsync(
                new CreateUserCommand(
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    request.Password!),
                cancellationToken);
        }
        catch (UserValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateUserValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (UserConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateUserConflict(
                    HttpContext,
                    exception.Message));
        }

        return Created(
            $"/api/v1/platform/users/{user.Id}",
            MapResponse(user));
    }

    /// <summary>Returns a global user through the platform administration scope.</summary>
    /// <remarks>
    /// Both platform-user endpoints are authorized exclusively through platform permissions.
    /// An organization-scoped assignment of <c>user.read</c> or <c>user.manage</c> is not
    /// sufficient. See decisions D-27 and D-37. Credential data is never read or returned.
    /// </remarks>
    /// <param name="userId">The global user identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested user without credential data.</returns>
    /// <response code="200">Returns the requested user.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide user.read permission.</response>
    /// <response code="404">No user exists with the supplied identifier.</response>
    [HttpGet("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformUserRead)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await queryService.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateUserNotFound(HttpContext));
        }

        return Ok(MapResponse(user));
    }

    private static UserResponse MapResponse(UserDetails user) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.CreatedAt);
}
