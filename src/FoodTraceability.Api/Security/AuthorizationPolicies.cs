namespace FoodTraceability.Api.Security;

public static class AuthorizationPolicies
{
    public const string ActiveUser = "ActiveUser";
    public const string OrganizationManage = "OrganizationManage";
    public const string OrganizationRead = "OrganizationRead";
}
