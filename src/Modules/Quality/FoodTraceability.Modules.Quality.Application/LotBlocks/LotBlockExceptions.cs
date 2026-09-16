namespace FoodTraceability.Modules.Quality.Application.LotBlocks;

public sealed class LotBlockValidationException : Exception
{
    public LotBlockValidationException(string message) : base(message)
    {
    }
}

public sealed class LotBlockNotFoundException : Exception
{
    public LotBlockNotFoundException() : base("The lot does not exist in this organization.")
    {
    }
}

public sealed class LotBlockConflictException : Exception
{
    public LotBlockConflictException(string message) : base(message)
    {
    }
}
