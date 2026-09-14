namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed class LabResultSampleNotFoundException : Exception
{
    public LabResultSampleNotFoundException()
        : base("Sample not found.")
    {
    }
}

public sealed class LabResultValidationException : Exception
{
    public LabResultValidationException(string message)
        : base(message)
    {
    }
}

public sealed class LabResultConflictException : Exception
{
    public LabResultConflictException(string message)
        : base(message)
    {
    }
}
