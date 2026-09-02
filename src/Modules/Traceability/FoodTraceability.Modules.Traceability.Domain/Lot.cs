namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class Lot
{
    public const int MaximumLotNumberLength = 100;

    private Lot(
        Guid id,
        Guid organizationId,
        string lotNumber,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        LotNumber = lotNumber;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public string LotNumber { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Lot Create(
        Guid id,
        Guid organizationId,
        string? lotNumber,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new TraceabilityDomainException("Lot id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Lot organization id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lotNumber))
        {
            throw new TraceabilityDomainException(
                "Lot number must not be null, empty, or consist only of whitespace.");
        }

        var normalizedLotNumber = lotNumber.Trim();
        if (normalizedLotNumber.Length > MaximumLotNumberLength)
        {
            throw new TraceabilityDomainException(
                $"Lot number must not exceed {MaximumLotNumberLength} characters.");
        }

        return new Lot(id, organizationId, normalizedLotNumber, createdAt);
    }
}
