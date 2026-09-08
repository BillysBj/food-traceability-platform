namespace FoodTraceability.Api.Security;

public static class AuthorizationPolicies
{
    public const string ActiveUser = "ActiveUser";
    public const string ArticleCreate = "ArticleCreate";
    public const string ArticleRead = "ArticleRead";
    public const string LotCreate = "LotCreate";
    public const string LotRead = "LotRead";
    public const string OrganizationManage = "OrganizationManage";
    public const string OrganizationRead = "OrganizationRead";
    public const string PlatformOrganizationManage = "PlatformOrganizationManage";
    public const string PlatformProductCreate = "PlatformProductCreate";
    public const string PlatformProductRead = "PlatformProductRead";
    public const string PlatformUserManage = "PlatformUserManage";
    public const string PlatformUserRead = "PlatformUserRead";
}
