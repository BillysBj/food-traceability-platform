namespace FoodTraceability.Modules.Traceability.Application.Traces;

public sealed class TraceTooLargeException(int maxNodes)
    : Exception($"The trace exceeds the configured limit of {maxNodes} nodes. No partial graph is returned.");
