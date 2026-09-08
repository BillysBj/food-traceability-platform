using FoodTraceability.Api.Contracts.Memberships;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Identity.Application.Memberships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/platform/organizations/{organizationId:guid}/members")]
public sealed class PlatformOrganizationMembersController(
    AddMemberService addMemberService,
    AssignRoleService assignRoleService,
    MembershipQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Adds a global user as a member of an organization.</summary>
    /// <remarks>
    /// This resource uses the <c>/platform</c> prefix because D-27 requires every endpoint under
    /// <c>/api/v1/organizations/{organizationId}/...</c> to use an organization role assignment
    /// for that exact organization. ORG-003 is deliberately restricted to platform
    /// administrators. Future self-management by organization administrators would therefore
    /// be a separate endpoint under <c>/api/v1/organizations/{organizationId}/members</c>.
    /// </remarks>
    /// <param name="organizationId">The organization to which the user is added.</param>
    /// <param name="request">The global user identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created membership without location scope.</returns>
    /// <response code="201">Returns the newly created membership.</response>
    /// <response code="400">The user or organization reference is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide user.manage permission.</response>
    /// <response code="409">The user is already a member of the organization.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformUserManage)]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MembershipResponse>> AddMember(
        Guid organizationId,
        AddMemberRequest request,
        CancellationToken cancellationToken)
    {
        MembershipDetails membership;
        try
        {
            membership = await addMemberService.AddAsync(
                new AddMemberCommand(organizationId, request.UserId),
                cancellationToken);
        }
        catch (MembershipValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (MembershipConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipConflict(
                    HttpContext,
                    exception.Message));
        }

        return Created(MemberPath(organizationId, membership.UserId), MapResponse(membership));
    }

    /// <summary>Returns one membership and its organization-wide role assignments.</summary>
    /// <remarks>
    /// This resource uses the <c>/platform</c> prefix because D-27 requires every endpoint under
    /// <c>/api/v1/organizations/{organizationId}/...</c> to use an organization role assignment
    /// for that exact organization. ORG-003 is deliberately restricted to platform
    /// administrators. Future self-management by organization administrators would therefore
    /// be a separate endpoint under <c>/api/v1/organizations/{organizationId}/members</c>.
    /// </remarks>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="userId">The member's global user identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The membership and its assigned role codes, without location scope.</returns>
    /// <response code="200">Returns the membership and its assigned roles.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide user.read permission.</response>
    /// <response code="404">The user is not a member of the organization.</response>
    [HttpGet("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformUserRead)]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipResponse>> GetById(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await queryService.FindAsync(
            organizationId,
            userId,
            cancellationToken);
        if (membership is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipNotFound(HttpContext));
        }

        return Ok(MapResponse(membership));
    }

    /// <summary>Assigns an organization-wide role to an existing member.</summary>
    /// <remarks>
    /// This resource uses the <c>/platform</c> prefix because D-27 requires every endpoint under
    /// <c>/api/v1/organizations/{organizationId}/...</c> to use an organization role assignment
    /// for that exact organization. ORG-003 is deliberately restricted to platform
    /// administrators. Future self-management by organization administrators would therefore
    /// be a separate endpoint under <c>/api/v1/organizations/{organizationId}/members</c>.
    /// Location-scoped assignments are not exposed by this endpoint.
    /// </remarks>
    /// <param name="organizationId">The organization identifier.</param>
    /// <param name="userId">The member's global user identifier.</param>
    /// <param name="request">The organization-role identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The updated membership and all its assigned role codes.</returns>
    /// <response code="201">Returns the updated membership.</response>
    /// <response code="400">The role reference is invalid or is not organization-assignable.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide user.manage permission.</response>
    /// <response code="404">The user is not a member of the organization.</response>
    /// <response code="409">The role is already assigned in the organization.</response>
    [HttpPost("{userId:guid}/roles")]
    [Authorize(Policy = AuthorizationPolicies.PlatformUserManage)]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MembershipResponse>> AssignRole(
        Guid organizationId,
        Guid userId,
        AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await assignRoleService.AssignAsync(
                new AssignRoleCommand(organizationId, userId, request.RoleId),
                cancellationToken);
        }
        catch (MembershipValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (MembershipNotFoundException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipNotFound(
                    HttpContext,
                    exception.Message));
        }
        catch (MembershipConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipConflict(
                    HttpContext,
                    exception.Message));
        }

        var membership = await queryService.FindAsync(
            organizationId,
            userId,
            cancellationToken);
        if (membership is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateMembershipNotFound(HttpContext));
        }

        return Created(MemberPath(organizationId, userId), MapResponse(membership));
    }

    private static string MemberPath(Guid organizationId, Guid userId) =>
        $"/api/v1/platform/organizations/{organizationId}/members/{userId}";

    private static MembershipResponse MapResponse(MembershipDetails membership) =>
        new(
            membership.OrganizationId,
            membership.UserId,
            membership.CreatedAt,
            membership.Roles
                .Select(role => new AssignedRoleResponse(
                    role.RoleId,
                    role.RoleCode,
                    role.CreatedAt))
                .ToArray());
}
