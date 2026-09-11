namespace FoodTraceability.Modules.Quality.Domain;

// No display name while D-07 remains open, following D-33's catalog.unit precedent
// and D-17/D-18's omission of seeded display prose. Translation storage is undecided.
public sealed class Parameter
{
    // 128 characters accommodate technical standard identifiers including edition,
    // part and amendment suffixes; this is a storage bound, not a method whitelist.
    public const int MaximumStandardMethodLength = 128;

    private Parameter(Guid id, ParameterCode code, Guid? unitId, string standardMethod)
    {
        Id = id;
        Code = code;
        UnitId = unitId;
        StandardMethod = standardMethod;
    }

    public Guid Id { get; }

    public ParameterCode Code { get; }

    public Guid? UnitId { get; }

    // Technical designation (e.g. ISO 660), not localized display text (D-07).
    public string StandardMethod { get; }

    public static Parameter Create(
        Guid id,
        ParameterCode? code,
        Guid? unitId,
        string? standardMethod)
    {
        if (id == Guid.Empty)
        {
            throw new QualityDomainException("Parameter id must not be empty.");
        }

        if (code is null)
        {
            throw new QualityDomainException("Parameter code must be provided.");
        }

        if (unitId == Guid.Empty)
        {
            throw new QualityDomainException("Parameter unit id must not be empty when provided.");
        }

        if (string.IsNullOrWhiteSpace(standardMethod))
        {
            throw new QualityDomainException(
                "Parameter standard method must not be null, empty, or consist only of whitespace.");
        }

        var normalizedMethod = standardMethod.Trim();
        if (normalizedMethod.Length > MaximumStandardMethodLength)
        {
            throw new QualityDomainException(
                $"Parameter standard method must not exceed {MaximumStandardMethodLength} characters.");
        }

        return new Parameter(id, code, unitId, normalizedMethod);
    }
}
