namespace FoodTraceability.Modules.Identity.Domain;

public enum RoleAssignmentScope
{
    Platform,
    Organization,
}

public static class RoleAssignmentScopeCodes
{
    public const string Platform = "PLATFORM";
    public const string Organization = "ORGANIZATION";
    public const int MaximumLength = 12;

    public static string ToCode(RoleAssignmentScope scope)
    {
        return scope switch
        {
            RoleAssignmentScope.Platform => Platform,
            RoleAssignmentScope.Organization => Organization,
            _ => throw new IdentityDomainException("Role assignment scope is invalid."),
        };
    }

    public static RoleAssignmentScope FromCode(string code)
    {
        return code switch
        {
            Platform => RoleAssignmentScope.Platform,
            Organization => RoleAssignmentScope.Organization,
            _ => throw new IdentityDomainException("Role assignment scope code is invalid."),
        };
    }
}
