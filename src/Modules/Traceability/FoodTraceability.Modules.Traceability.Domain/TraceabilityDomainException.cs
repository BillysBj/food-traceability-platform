namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class TraceabilityDomainException : Exception
{
    public TraceabilityDomainException(string message)
        : base(message)
    {
    }
}
