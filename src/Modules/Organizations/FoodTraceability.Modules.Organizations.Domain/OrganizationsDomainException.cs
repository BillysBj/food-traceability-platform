namespace FoodTraceability.Modules.Organizations.Domain;

public sealed class OrganizationsDomainException : Exception
{
    public OrganizationsDomainException(string message)
        : base(message)
    {
    }
}
