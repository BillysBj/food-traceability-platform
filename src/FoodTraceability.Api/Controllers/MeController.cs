using FoodTraceability.Api.Contracts.Authorization;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
public sealed class MeController(EffectiveAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>Returns the authenticated identity and effective permissions by assignment source.</summary>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The current user and separately resolved platform, organization, and location permissions.</returns>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ActiveUser)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken cancellationToken)
    {
        var subjectValue = User.FindFirst("sub")?.Value;
        var authorization = Guid.TryParse(subjectValue, out var userId)
            ? await authorizationService.ResolveAsync(userId, cancellationToken)
            : null;
        if (authorization is not { IsActive: true })
        {
            return Unauthorized(CreateUnauthorizedProblemDetails());
        }

        return Ok(new MeResponse(
            authorization.UserId,
            authorization.Email,
            authorization.FirstName,
            authorization.LastName,
            authorization.PlatformPermissions,
            authorization.OrganizationPermissions
                .Select(permissionSet => new OrganizationPermissionsResponse(
                    permissionSet.OrganizationId,
                    permissionSet.Permissions))
                .ToArray(),
            authorization.LocationPermissions
                .Select(permissionSet => new LocationPermissionsResponse(
                    permissionSet.OrganizationId,
                    permissionSet.LocationId,
                    permissionSet.Permissions))
                .ToArray()));
    }

    private static ProblemDetails CreateUnauthorizedProblemDetails()
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication is required.",
        };
        problemDetails.Extensions["errorCode"] = "AUTHENTICATION_REQUIRED";
        return problemDetails;
    }
}
