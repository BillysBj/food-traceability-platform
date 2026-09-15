namespace FoodTraceability.Modules.Quality.Domain;

public sealed class SpecificationParameter
{
    public const int ValuePrecision = 18;
    public const int ValueScale = 6;

    private SpecificationParameter(
        Guid id,
        Guid specificationId,
        Guid parameterId,
        decimal? minimum,
        decimal? maximum,
        decimal? target,
        bool required,
        DateTimeOffset createdAt)
    {
        Id = id;
        SpecificationId = specificationId;
        ParameterId = parameterId;
        Minimum = minimum;
        Maximum = maximum;
        Target = target;
        Required = required;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid SpecificationId { get; }

    public Guid ParameterId { get; }

    public decimal? Minimum { get; }

    public decimal? Maximum { get; }

    public decimal? Target { get; }

    public bool Required { get; }

    public DateTimeOffset CreatedAt { get; }

    public static SpecificationParameter Create(
        Guid id,
        Guid specificationId,
        Guid parameterId,
        decimal? minimum,
        decimal? maximum,
        decimal? target,
        bool required,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Specification parameter id must not be empty.");
        }

        if (specificationId == Guid.Empty)
        {
            throw new QualityDomainException("Specification parameter specification id must not be empty.");
        }

        if (parameterId == Guid.Empty)
        {
            throw new QualityDomainException("Specification parameter parameter id must not be empty.");
        }

        if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
        {
            throw new QualityDomainException("Specification parameter minimum must not exceed maximum.");
        }

        // D-52: these are reference values, not an automatic assessment rule.
        // In particular, Target need not lie between Minimum and Maximum.
        // Preserve the supplied timestamp, as with Sample and LabResult.
        return new SpecificationParameter(
            id, specificationId, parameterId, minimum, maximum, target, required, createdAt);
    }
}
