namespace FoodTraceability.Modules.Traceability.Domain;

public sealed class TraceabilityEvent
{
    public const int MaximumExternalReferenceLength = 128;
    public const int MaximumDescriptionLength = 2000;

    private readonly List<EventInput> _inputs;
    private readonly List<EventOutput> _outputs;

    private TraceabilityEvent(
        Guid id,
        Guid eventTypeId,
        Guid organizationId,
        Guid locationId,
        DateTimeOffset occurredAt,
        string? externalReference,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt,
        IReadOnlyList<EventInput> inputs,
        IReadOnlyList<EventOutput> outputs)
    {
        Id = id;
        EventTypeId = eventTypeId;
        OrganizationId = organizationId;
        LocationId = locationId;
        OccurredAt = occurredAt;
        ExternalReference = externalReference;
        Description = description;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        _inputs = [.. inputs];
        _outputs = [.. outputs];
    }

    // Used exclusively by EF Core when loading an already persisted event. The invariants
    // were enforced by Create when the event was originally created, as they are for Lot.
    private TraceabilityEvent(
        Guid id,
        Guid eventTypeId,
        Guid organizationId,
        Guid locationId,
        DateTimeOffset occurredAt,
        string? externalReference,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        EventTypeId = eventTypeId;
        OrganizationId = organizationId;
        LocationId = locationId;
        OccurredAt = occurredAt;
        ExternalReference = externalReference;
        Description = description;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        _inputs = [];
        _outputs = [];
    }

    public Guid Id { get; }

    public Guid EventTypeId { get; }

    public Guid OrganizationId { get; }

    public Guid LocationId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string? ExternalReference { get; }

    public string? Description { get; }

    public Guid CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<EventInput> Inputs => _inputs.AsReadOnly();

    public IReadOnlyList<EventOutput> Outputs => _outputs.AsReadOnly();

    public static TraceabilityEvent Create(
        Guid id,
        Guid eventTypeId,
        Guid organizationId,
        Guid locationId,
        DateTimeOffset occurredAt,
        string? externalReference,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt,
        IReadOnlyList<EventInput>? inputs,
        IReadOnlyList<EventOutput>? outputs)
    {
        if (id == Guid.Empty)
        {
            throw new TraceabilityDomainException("Traceability event id must not be empty.");
        }

        if (eventTypeId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Traceability event type id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Traceability event organization id must not be empty.");
        }

        if (locationId == Guid.Empty)
        {
            throw new TraceabilityDomainException("Traceability event location id must not be empty.");
        }

        if (createdBy == Guid.Empty)
        {
            throw new TraceabilityDomainException("Traceability event created by id must not be empty.");
        }

        if (inputs is null)
        {
            throw new TraceabilityDomainException("Traceability event inputs must not be null.");
        }

        if (outputs is null)
        {
            throw new TraceabilityDomainException("Traceability event outputs must not be null.");
        }

        if (inputs.Count == 0 && outputs.Count == 0)
        {
            throw new TraceabilityDomainException(
                "Traceability event must have at least one input or output.");
        }

        var copiedInputs = ValidateAndCopyInputs(inputs);
        var copiedOutputs = ValidateAndCopyOutputs(outputs);

        var inputLotIds = copiedInputs.Select(input => input.LotId).ToHashSet();
        foreach (var output in copiedOutputs)
        {
            if (inputLotIds.Contains(output.LotId))
            {
                throw new TraceabilityDomainException(
                    $"Lot '{output.LotId}' must not be both an input and an output of the same event.");
            }
        }

        return new TraceabilityEvent(
            id,
            eventTypeId,
            organizationId,
            locationId,
            occurredAt,
            NormalizeOptionalText(
                externalReference,
                MaximumExternalReferenceLength,
                "Traceability event external reference"),
            NormalizeOptionalText(
                description,
                MaximumDescriptionLength,
                "Traceability event description"),
            createdBy,
            createdAt,
            copiedInputs,
            copiedOutputs);
    }

    private static IReadOnlyList<EventInput> ValidateAndCopyInputs(
        IReadOnlyList<EventInput> inputs)
    {
        var copiedInputs = new EventInput[inputs.Count];
        var lotIds = new HashSet<Guid>();

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            if (input is null)
            {
                throw new TraceabilityDomainException(
                    "Traceability event inputs must not contain null entries.");
            }

            if (!lotIds.Add(input.LotId))
            {
                throw new TraceabilityDomainException(
                    "Traceability event inputs must not contain duplicate lot ids.");
            }

            copiedInputs[index] = input;
        }

        return Array.AsReadOnly(copiedInputs);
    }

    private static IReadOnlyList<EventOutput> ValidateAndCopyOutputs(
        IReadOnlyList<EventOutput> outputs)
    {
        var copiedOutputs = new EventOutput[outputs.Count];
        var lotIds = new HashSet<Guid>();

        for (var index = 0; index < outputs.Count; index++)
        {
            var output = outputs[index];
            if (output is null)
            {
                throw new TraceabilityDomainException(
                    "Traceability event outputs must not contain null entries.");
            }

            if (!lotIds.Add(output.LotId))
            {
                throw new TraceabilityDomainException(
                    "Traceability event outputs must not contain duplicate lot ids.");
            }

            copiedOutputs[index] = output;
        }

        return Array.AsReadOnly(copiedOutputs);
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string fieldName)
    {
        if (value is null)
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new TraceabilityDomainException(
                $"{fieldName} must be null or contain non-whitespace characters.");
        }

        if (normalizedValue.Length > maximumLength)
        {
            throw new TraceabilityDomainException(
                $"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
