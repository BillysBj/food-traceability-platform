using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FoodTraceability.Api.Security;

public static class ApiAuthorizationConfiguration
{
    private const string OrganizationReadPermission = "organization.read";

    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.ActiveUser,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ActiveUserRequirement()));
            options.AddPolicy(
                AuthorizationPolicies.OrganizationRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(OrganizationReadPermission)));
        });
        services.AddScoped<IAuthorizationHandler, DatabaseAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            ProblemDetailsAuthorizationResultHandler>();

        return services;
    }
}
