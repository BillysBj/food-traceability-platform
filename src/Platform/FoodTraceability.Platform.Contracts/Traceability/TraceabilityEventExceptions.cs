namespace FoodTraceability.Platform.Contracts.Traceability;

public sealed class TraceabilityEventValidationException : Exception
{
    public TraceabilityEventValidationException(string message)
        : base(message)
    {
    }
}

public sealed class TraceabilityEventConflictException : Exception
{
    public TraceabilityEventConflictException(string message)
        : base(message)
    {
    }
}
