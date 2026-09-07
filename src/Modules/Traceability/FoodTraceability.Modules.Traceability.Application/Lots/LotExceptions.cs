namespace FoodTraceability.Modules.Traceability.Application.Lots;

public sealed class LotValidationException : Exception
{
    public LotValidationException(string message)
        : base(message)
    {
    }
}

public sealed class LotConflictException : Exception
{
    public LotConflictException(string message)
        : base(message)
    {
    }
}
