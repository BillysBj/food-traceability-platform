namespace FoodTraceability.Modules.Quality.Domain;

public sealed class Sample
{
    public const int MaximumSampleNumberLength = 100;

    private Sample(
        Guid id,
        Guid organizationId,
        Guid lotId,
        Guid locationId,
        Guid traceabilityEventId,
        string sampleNumber,
        DateTimeOffset takenAt,
        SampleStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        LotId = lotId;
        LocationId = locationId;
        TraceabilityEventId = traceabilityEventId;
        SampleNumber = sampleNumber;
        TakenAt = takenAt;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid OrganizationId { get; }

    public Guid LotId { get; }

    public Guid LocationId { get; }

    public Guid TraceabilityEventId { get; }

    public string SampleNumber { get; }

    public DateTimeOffset TakenAt { get; }

    public SampleStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Sample Create(
        Guid id,
        Guid organizationId,
        Guid lotId,
        Guid locationId,
        Guid traceabilityEventId,
        string? sampleNumber,
        DateTimeOffset takenAt,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Sample id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new QualityDomainException("Sample organization id must not be empty.");
        }

        if (lotId == Guid.Empty)
        {
            throw new QualityDomainException("Sample lot id must not be empty.");
        }

        if (locationId == Guid.Empty)
        {
            throw new QualityDomainException("Sample location id must not be empty.");
        }

        if (traceabilityEventId == Guid.Empty)
        {
            throw new QualityDomainException("Sample traceability event id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(sampleNumber))
        {
            throw new QualityDomainException(
                "Sample number must not be null, empty, or consist only of whitespace.");
        }

        var normalizedSampleNumber = sampleNumber.Trim();
        if (normalizedSampleNumber.Length > MaximumSampleNumberLength)
        {
            throw new QualityDomainException(
                $"Sample number must not exceed {MaximumSampleNumberLength} characters.");
        }

        // As with Lot, the caller supplies the timestamps. D-36 normalization belongs
        // to the Application service (QLT-002), with persistence conventions as a fallback.
        return new Sample(
            id,
            organizationId,
            lotId,
            locationId,
            traceabilityEventId,
            normalizedSampleNumber,
            takenAt,
            SampleStatus.Pending,
            createdAt);
    }
}
