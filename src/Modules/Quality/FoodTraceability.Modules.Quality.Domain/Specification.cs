namespace FoodTraceability.Modules.Quality.Domain;

public sealed class Specification
{
    private Specification(
        Guid id,
        Guid articleId,
        int version,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset createdAt)
    {
        Id = id;
        ArticleId = articleId;
        Version = version;
        ValidFrom = validFrom;
        ValidTo = validTo;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid ArticleId { get; }

    public int Version { get; }

    public DateTimeOffset ValidFrom { get; }

    // Null represents a specification with no end date.
    public DateTimeOffset? ValidTo { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Specification Create(
        Guid id,
        Guid articleId,
        int version,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Specification id must not be empty.");
        }

        if (articleId == Guid.Empty)
        {
            throw new QualityDomainException("Specification article id must not be empty.");
        }

        if (version <= 0)
        {
            throw new QualityDomainException("Specification version must be greater than zero.");
        }

        if (validTo.HasValue && validTo.Value < validFrom)
        {
            throw new QualityDomainException("Specification valid to must not precede valid from.");
        }

        // As with Sample and LabResult, timestamp normalization belongs to the caller,
        // with persistence conventions as a fallback. Keep the supplied values here.
        return new Specification(id, articleId, version, validFrom, validTo, createdAt);
    }
}
