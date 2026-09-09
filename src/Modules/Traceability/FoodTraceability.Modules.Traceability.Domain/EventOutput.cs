namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class EventOutput
{
    private EventOutput(Guid id, Guid lotId, decimal quantity, Guid unitId)
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

    public static EventOutput Create(Guid id, Guid lotId, decimal quantity, Guid unitId)
    {
        if (id == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event output id must not be empty.");
        }

        if (lotId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event output lot id must not be empty.");
        }

        if (quantity <= 0)
        {
            throw new TraceabilityDomainException("Event output quantity must be greater than zero.");
        }

        if (unitId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Event output unit id must not be empty.");
        }

        return new EventOutput(id, lotId, quantity, unitId);
    }
}
