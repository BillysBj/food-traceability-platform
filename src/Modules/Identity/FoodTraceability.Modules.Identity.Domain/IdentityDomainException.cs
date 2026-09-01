namespace FoodTraceability.Modules.Identity.Domain;

public sealed class IdentityDomainException : Exception
{
    public IdentityDomainException(string message)
        : base(message)
    {
    }
}
