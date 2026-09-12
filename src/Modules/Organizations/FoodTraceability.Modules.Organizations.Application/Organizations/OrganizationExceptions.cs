namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class OrganizationConflictException : Exception
{
    public OrganizationConflictException(string message)
        : base(message)
    {
    }
}

public sealed class OrganizationValidationException : Exception
{
    public OrganizationValidationException(string message)
        : base(message)
    {
    }
}
