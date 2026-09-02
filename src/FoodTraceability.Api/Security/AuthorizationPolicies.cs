namespace FoodTraceability.Api.Security;

public static class AuthorizationPolicies
{
    public const string ActiveUser = "ActiveUser";
    public const string ArticleCreate = "ArticleCreate";
    public const string ArticleRead = "ArticleRead";
    public const string OrganizationManage = "OrganizationManage";
    public const string OrganizationRead = "OrganizationRead";
}
