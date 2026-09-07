namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class Lot
{
    public const int MaximumLotNumberLength = 100;

    private Lot(
        Guid id,
        Guid organizationId,
        Guid articleId,
        string lotNumber,
        decimal quantity,
        Guid unitId,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        ArticleId = articleId;
        LotNumber = lotNumber;
        Quantity = quantity;
        UnitId = unitId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid ArticleId { get; }

    public string LotNumber { get; }

    /// <summary>
    /// The initial production quantity of the lot. It is not a running stock balance and is
    /// not overwritten as the lot is consumed; the available stock is derived from
    /// traceability events instead. See D-32.
    /// </summary>
    public decimal Quantity { get; }

    public Guid UnitId { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Lot Create(
        Guid id,
        Guid organizationId,
        Guid articleId,
        string? lotNumber,
        decimal quantity,
        Guid unitId,
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

        if (articleId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Lot article id must not be empty.");
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

        if (quantity <= 0)
        {
            throw new TraceabilityDomainException("Lot quantity must be greater than zero.");
        }

        if (unitId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Lot unit id must not be empty.");
        }

        return new Lot(
            id,
            organizationId,
            articleId,
            normalizedLotNumber,
            quantity,
            unitId,
            createdAt);
    }
}
