using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FoodTraceability.Api.Security;

public static class ApiAuthorizationConfiguration
{
    private const string ArticleCreatePermission = "article.create";
    private const string ArticleReadPermission = "article.read";
    private const string LotCreatePermission = "lot.create";
    private const string LotReadPermission = "lot.read";
    private const string OrganizationReadPermission = "organization.read";
    private const string OrganizationManagePermission = "organization.manage";
    private const string ProductCreatePermission = "product.create";
    private const string ProductReadPermission = "product.read";
    private const string TraceabilityEventCreatePermission = "trace.event.create";
    private const string TraceabilityReadPermission = "trace.read";
    private const string UserManagePermission = "user.manage";
    private const string UserReadPermission = "user.read";

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
                AuthorizationPolicies.LotCreate,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(LotCreatePermission)));
            options.AddPolicy(
                AuthorizationPolicies.LotRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(LotReadPermission)));
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
            options.AddPolicy(
                AuthorizationPolicies.PlatformOrganizationManage,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PlatformPermissionRequirement(OrganizationManagePermission)));
            options.AddPolicy(
                AuthorizationPolicies.PlatformProductCreate,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PlatformPermissionRequirement(ProductCreatePermission)));
            options.AddPolicy(
                AuthorizationPolicies.PlatformProductRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PlatformPermissionRequirement(ProductReadPermission)));
            options.AddPolicy(
                AuthorizationPolicies.PlatformUserManage,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PlatformPermissionRequirement(UserManagePermission)));
            options.AddPolicy(
                AuthorizationPolicies.PlatformUserRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new PlatformPermissionRequirement(UserReadPermission)));
            options.AddPolicy(
                AuthorizationPolicies.TraceabilityEventCreate,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(
                            TraceabilityEventCreatePermission)));
            options.AddPolicy(
                AuthorizationPolicies.TraceabilityRead,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new OrganizationPermissionRequirement(TraceabilityReadPermission)));
        });
        services.AddScoped<IAuthorizationHandler, DatabaseAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            ProblemDetailsAuthorizationResultHandler>();

        return services;
    }
}
