using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FoodTraceability.Api.Security;

public static class ApiAuthorizationConfiguration
{
    private const string ArticleCreatePermission = "article.create";
    private const string ArticleReadPermission = "article.read";
    private const string OrganizationReadPermission = "organization.read";
    private const string OrganizationManagePermission = "organization.manage";

    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.ArticleCreate,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(ArticleCreatePermission)));
            options.AddPolicy(
                AuthorizationPolicies.ArticleRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(ArticleReadPermission)));
            options.AddPolicy(
                AuthorizationPolicies.ActiveUser,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ActiveUserRequirement()));
            options.AddPolicy(
                AuthorizationPolicies.OrganizationManage,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(OrganizationManagePermission)));
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
