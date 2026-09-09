namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class EventInput
{
    private EventInput(Guid id, Guid lotId, decimal quantity, Guid unitId)
    {
        Id = id;
        LotId = lotId;
        Quantity = quantity;
        UnitId = unitId;
    }

    public Guid Id { get; }

    public Guid LotId { get; }

    public decimal Quantity { get; }

    /// <summary>The unit must match the referenced lot's unit; TRC-007 enforces this structurally.</summary>
    public Guid UnitId { get; }

    public static EventInput Create(Guid id, Guid lotId, decimal quantity, Guid unitId)
    {
        if (id == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event input id must not be empty.");
        }

        if (lotId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event input lot id must not be empty.");
        }

        if (quantity <= 0)
        {
            throw new TraceabilityDomainException("Event input quantity must be greater than zero.");
        }

        if (unitId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event input unit id must not be empty.");
        }

        return new EventInput(id, lotId, quantity, unitId);
    }
}
