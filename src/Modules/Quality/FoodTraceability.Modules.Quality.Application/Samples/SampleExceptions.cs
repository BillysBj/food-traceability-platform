namespace FoodTraceability.Modules.Quality.Application.Samples;

public sealed class SampleValidationException : Exception
{
    public SampleValidationException(string message)
        : base(message)
    {
    }
}

public sealed class SampleConflictException : Exception
{
    public SampleConflictException(string message)
        : base(message)
    {
    }
}
