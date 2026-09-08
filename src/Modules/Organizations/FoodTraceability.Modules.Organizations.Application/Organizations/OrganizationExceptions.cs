namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class OrganizationValidationException : Exception
{
    public OrganizationValidationException(string message)
        : base(message)
    {
    }
}
