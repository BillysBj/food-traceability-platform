using Microsoft.AspNetCore.Authorization;

namespace FoodTraceability.Api.Security;

internal sealed class ActiveUserRequirement : IAuthorizationRequirement;

internal sealed record OrganizationPermissionRequirement(string PermissionCode)
    : IAuthorizationRequirement;
