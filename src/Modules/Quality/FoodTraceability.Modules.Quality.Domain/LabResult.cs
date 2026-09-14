namespace FoodTraceability.Modules.Quality.Domain;

public sealed class LabResult
{
    // Like Parameter.StandardMethod, this is a technical method designation,
    // including edition/part suffixes, rather than a description or whitelist.
    public const int MaximumMethodLength = 128;
    public const int ValuePrecision = 18;
    public const int ValueScale = 6;
    private const decimal MaximumAbsoluteValue = 999999999999.999999m;

    private LabResult(
        Guid id,
        Guid sampleId,
        Guid parameterId,
        decimal value,
        LabResultAssessment assessment,
        string method,
        DateTimeOffset measuredAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        SampleId = sampleId;
        ParameterId = parameterId;
        Value = value;
        Assessment = assessment;
        Method = method;
        MeasuredAt = measuredAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid SampleId { get; }

    public Guid ParameterId { get; }

    public decimal Value { get; }

    public LabResultAssessment Assessment { get; }

    public string Method { get; }

    public DateTimeOffset MeasuredAt { get; }

    public DateTimeOffset CreatedAt { get; }

    public static LabResult Create(
        Guid id,
        Guid sampleId,
        Guid parameterId,
        decimal value,
        LabResultAssessment assessment,
        string? method,
        DateTimeOffset measuredAt,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Lab result id must not be empty.");
        }

        if (sampleId == Guid.Empty)
        {
            throw new QualityDomainException("Lab result sample id must not be empty.");
        }

        if (parameterId == Guid.Empty)
        {
            throw new QualityDomainException("Lab result parameter id must not be empty.");
        }

        // Only storage representability is enforced: negative values and zero are valid.
        // Comparing the rounded value also accepts insignificant trailing zeroes.
        if (value < -MaximumAbsoluteValue || value > MaximumAbsoluteValue
            || decimal.Round(value, ValueScale) != value)
        {
            throw new QualityDomainException(
                $"Lab result value must fit precision {ValuePrecision} and scale {ValueScale} without rounding.");
        }

        if (!Enum.IsDefined(assessment))
        {
            throw new QualityDomainException("Lab result assessment must be Pass or Fail.");
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new QualityDomainException(
                "Lab result method must not be null, empty, or consist only of whitespace.");
        }

        var normalizedMethod = method.Trim();
        if (normalizedMethod.Length > MaximumMethodLength)
        {
            throw new QualityDomainException(
                $"Lab result method must not exceed {MaximumMethodLength} characters.");
        }

        // As with Sample, D-36 timestamp normalization belongs to the caller's
        // Application service (QLT-003), with persistence conventions as a fallback.
        return new LabResult(id, sampleId, parameterId, value, assessment, normalizedMethod, measuredAt, createdAt);
    }
}
