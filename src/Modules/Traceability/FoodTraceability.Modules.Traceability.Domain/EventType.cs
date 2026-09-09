namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class EventType
{
    private EventType(
        Guid id,
        EventTypeCode code,
        EventTypeClassification classification,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        Classification = classification;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public EventTypeCode Code { get; }

    public EventTypeClassification Classification { get; }

    public DateTimeOffset CreatedAt { get; }

    public static EventType Create(
        Guid id,
        EventTypeCode? code,
        EventTypeClassification classification,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event type id must not be empty.");
        }

        if (code is null)
        {
            throw new TraceabilityDomainException("Event type code must be provided.");
        }

        if (classification is not EventTypeClassification.Traceability
            and not EventTypeClassification.TraceabilityAwaitingLogistics
            and not EventTypeClassification.Logistics
            and not EventTypeClassification.Quality
            and not EventTypeClassification.Deferred)
        {
            throw new TraceabilityDomainException("Event type classification must be valid.");
        }

        return new EventType(id, code, classification, createdAt);
    }
}
