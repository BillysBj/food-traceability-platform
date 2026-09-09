namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class EventType
{
    private EventType(Guid id, EventTypeCode code, DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public EventTypeCode Code { get; }

    public DateTimeOffset CreatedAt { get; }

    public static EventType Create(
        Guid id,
        EventTypeCode? code,
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

        return new EventType(id, code, createdAt);
    }
}
