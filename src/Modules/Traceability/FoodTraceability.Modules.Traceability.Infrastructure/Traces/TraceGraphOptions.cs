namespace FoodTraceability.Modules.Traceability.Infrastructure.Traces;

public sealed class TraceGraphOptions
{
    public const string SectionName = "Traceability:Graph";

    public int MaxNodes { get; set; } = 1000;
}
