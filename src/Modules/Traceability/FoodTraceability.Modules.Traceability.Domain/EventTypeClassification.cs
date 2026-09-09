namespace FoodTraceability.Modules.Traceability.Domain;

public enum EventTypeClassification
{
    Traceability,
    TraceabilityAwaitingLogistics,
    Logistics,
    Quality,
    Deferred,
}

public static class EventTypeClassificationCodes
{
    public const string Traceability = "TRACEABILITY";
    public const string TraceabilityAwaitingLogistics = "TRACEABILITY_AWAITING_LOGISTICS";
    public const string Logistics = "LOGISTICS";
    public const string Quality = "QUALITY";
    public const string Deferred = "DEFERRED";
    public const int MaximumLength = 32;

    public static string ToCode(EventTypeClassification classification)
    {
        return classification switch
        {
            EventTypeClassification.Traceability => Traceability,
            EventTypeClassification.TraceabilityAwaitingLogistics => TraceabilityAwaitingLogistics,
            EventTypeClassification.Logistics => Logistics,
            EventTypeClassification.Quality => Quality,
            EventTypeClassification.Deferred => Deferred,
            _ => throw new TraceabilityDomainException("Event type classification is invalid."),
        };
    }

    public static EventTypeClassification FromCode(string code)
    {
        return code switch
        {
            Traceability => EventTypeClassification.Traceability,
            TraceabilityAwaitingLogistics => EventTypeClassification.TraceabilityAwaitingLogistics,
            Logistics => EventTypeClassification.Logistics,
            Quality => EventTypeClassification.Quality,
            Deferred => EventTypeClassification.Deferred,
            _ => throw new TraceabilityDomainException("Event type classification code is invalid."),
        };
    }
}
