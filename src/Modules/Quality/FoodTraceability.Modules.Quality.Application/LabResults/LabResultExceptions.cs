namespace FoodTraceability.Modules.Quality.Application.LabResults;

public sealed class AmbiguousSpecificationException : Exception
{
    public AmbiguousSpecificationException()
        : base("More than one specification applies to the sample's article and sampling time.")
    {
    }
}

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
