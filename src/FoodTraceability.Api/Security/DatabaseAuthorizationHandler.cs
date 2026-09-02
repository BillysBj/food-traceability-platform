using FoodTraceability.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace FoodTraceability.Api.Security;

internal sealed class DatabaseAuthorizationHandler(
    EffectiveAuthorizationService authorizationService) : IAuthorizationHandler
{
    public const string InvalidPrincipalFailureReason = "INVALID_PRINCIPAL";
    private const string OrganizationIdRouteValue = "organizationId";

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var requirements = context.PendingRequirements
            .Where(requirement => requirement is ActiveUserRequirement
                or OrganizationPermissionRequirement)
            .ToArray();
        if (requirements.Length == 0)
        {
            return;
        }

        var subjectValue = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subjectValue, out var userId))
        {
            context.Fail(new AuthorizationFailureReason(this, InvalidPrincipalFailureReason));
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;
        var authorization = await authorizationService.ResolveAsync(userId, cancellationToken);
        if (authorization is not { IsActive: true })
        {
            context.Fail(new AuthorizationFailureReason(this, InvalidPrincipalFailureReason));
            return;
        }

        foreach (var requirement in requirements)
        {
            switch (requirement)
            {
                case ActiveUserRequirement:
                    context.Succeed(requirement);
                    break;

                case OrganizationPermissionRequirement organizationRequirement
                    when HasOrganizationPermission(
                        context.Resource,
                        authorization,
                        organizationRequirement.PermissionCode):
                    context.Succeed(requirement);
                    break;
            }
        }
    }

    private static bool HasOrganizationPermission(
        object? resource,
        EffectiveAuthorization authorization,
        string permissionCode)
    {
        if (resource is not HttpContext httpContext
            || !httpContext.Request.RouteValues.TryGetValue(
                OrganizationIdRouteValue,
                out var routeValue)
            || !Guid.TryParse(Convert.ToString(routeValue), out var organizationId))
        {
            return false;
        }

        // OrganizationPermissionSets are created only from memberships and assignments whose
        // location_id is NULL. Platform and location-specific permission sources never enter
        // this check.
        return authorization.HasOrganizationMembership(organizationId)
            && authorization.HasOrganizationPermission(organizationId, permissionCode);
    }
}
