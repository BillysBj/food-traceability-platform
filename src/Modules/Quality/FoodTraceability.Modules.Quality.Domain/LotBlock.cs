namespace FoodTraceability.Modules.Quality.Domain;

public sealed class LotBlock
{
    public const int MaximumReasonLength = 2000;

    private LotBlock(
        Guid id,
        Guid organizationId,
        Guid lotId,
        string reason,
        DateTimeOffset blockedAt,
        Guid blockedBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        LotId = lotId;
        Reason = reason;
        BlockedAt = blockedAt;
        BlockedBy = blockedBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid OrganizationId { get; }
    public Guid LotId { get; }
    public string Reason { get; }
    public DateTimeOffset BlockedAt { get; }
    public Guid BlockedBy { get; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid? ReleasedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static LotBlock Create(
        Guid id,
        Guid organizationId,
        Guid lotId,
        string? reason,
        DateTimeOffset blockedAt,
        Guid blockedBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Lot block id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new QualityDomainException("Lot block organization id must not be empty.");
        }

        if (lotId == Guid.Empty)
        {
            throw new QualityDomainException("Lot block lot id must not be empty.");
        }

        if (blockedBy == Guid.Empty)
        {
            throw new QualityDomainException("Lot block blocked by must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new QualityDomainException(
                "Lot block reason must not be null, empty, or consist only of whitespace.");
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > MaximumReasonLength)
        {
            throw new QualityDomainException(
                $"Lot block reason must not exceed {MaximumReasonLength} characters.");
        }

        // D-36 normalization belongs to the caller/persistence, never the aggregate.
        return new LotBlock(id, organizationId, lotId, normalizedReason, blockedAt, blockedBy, createdAt);
    }

    public void Release(DateTimeOffset releasedAt, Guid releasedBy)
    {
        if (ReleasedAt is not null)
        {
            throw new QualityDomainException("Lot block has already been released.");
        }

        if (releasedBy == Guid.Empty)
        {
            throw new QualityDomainException("Lot block released by must not be empty.");
        }

        ReleasedAt = releasedAt;
        ReleasedBy = releasedBy;
    }
}
