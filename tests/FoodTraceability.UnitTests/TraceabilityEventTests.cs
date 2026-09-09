using System.Reflection;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class TraceabilityEventTests
{
    private static readonly Guid EventId =
        Guid.Parse("e2de4506-c692-414d-87c1-1fc3ef471264");
    private static readonly Guid EventTypeId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");
    private static readonly Guid OrganizationId =
        Guid.Parse("c4fa5949-3c76-48e8-beba-18021875597c");
    private static readonly Guid LocationId =
        Guid.Parse("0a49f008-6cbb-4438-8020-1e87b25a97bf");
    private static readonly Guid CreatedBy =
        Guid.Parse("d6c548c9-b18e-44d8-bc87-6b217ca65d50");
    private static readonly Guid InputId =
        Guid.Parse("ce054c29-f170-417d-8fa2-3b9cf7e24ebc");
    private static readonly Guid InputLotId =
        Guid.Parse("455c89d4-2f8f-45a6-905c-a1565ef33ae2");
    private static readonly Guid OutputId =
        Guid.Parse("ae2a6fc4-ac87-4484-bf41-57aa8eaf266a");
    private static readonly Guid OutputLotId =
        Guid.Parse("56b0d8e0-471b-47ba-9344-d50fdddf1a3e");
    private static readonly Guid UnitId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 9, 9, 10, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void EventWithOnlyInputsIsValidAndRetainsProvidedValues()
    {
        var input = CreateInput();

        var traceabilityEvent = CreateEvent([input], []);

        Assert.Equal(EventId, traceabilityEvent.Id);
        Assert.Equal(EventTypeId, traceabilityEvent.EventTypeId);
        Assert.Equal(OrganizationId, traceabilityEvent.OrganizationId);
        Assert.Equal(LocationId, traceabilityEvent.LocationId);
        Assert.Equal(OccurredAt, traceabilityEvent.OccurredAt);
        Assert.Equal("EXT-001", traceabilityEvent.ExternalReference);
        Assert.Equal("Pressing", traceabilityEvent.Description);
        Assert.Equal(CreatedBy, traceabilityEvent.CreatedBy);
        Assert.Equal(CreatedAt, traceabilityEvent.CreatedAt);
        Assert.Same(input, Assert.Single(traceabilityEvent.Inputs));
        Assert.Empty(traceabilityEvent.Outputs);
    }

    [Fact]
    public void EventWithOnlyOutputsIsValid()
    {
        var output = CreateOutput();

        var traceabilityEvent = CreateEvent([], [output]);

        Assert.Empty(traceabilityEvent.Inputs);
        Assert.Same(output, Assert.Single(traceabilityEvent.Outputs));
    }

    [Fact]
    public void EventWithInputsAndOutputsIsValid()
    {
        var input = CreateInput();
        var output = CreateOutput();

        var traceabilityEvent = CreateEvent([input], [output]);

        Assert.Same(input, Assert.Single(traceabilityEvent.Inputs));
        Assert.Same(output, Assert.Single(traceabilityEvent.Outputs));
    }

    [Fact]
    public void EventPublicStateContainsExactlyRequiredFieldsAndSides()
    {
        var expectedPropertyNames = new[]
        {
            "CreatedAt",
            "CreatedBy",
            "Description",
            "EventTypeId",
            "ExternalReference",
            "Id",
            "Inputs",
            "LocationId",
            "OccurredAt",
            "OrganizationId",
            "Outputs",
        };
        var actualPropertyNames = typeof(TraceabilityEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(expectedPropertyNames, actualPropertyNames);
    }

    [Fact]
    public void EventWithoutInputsOrOutputsIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent([], []));
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent(null, [CreateOutput()]));
    }

    [Fact]
    public void NullOutputsAreRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent([CreateInput()], null));
    }

    [Fact]
    public void NullInputEntryIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent([null!], []));
    }

    [Fact]
    public void NullOutputEntryIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent([], [null!]));
    }

    [Fact]
    public void DuplicateInputLotIsRejected()
    {
        var secondInput = CreateInput(
            id: Guid.Parse("41311537-8fa5-44e1-bc06-d9a070a4694c"));

        Assert.Throws<TraceabilityDomainException>(() =>
            CreateEvent([CreateInput(), secondInput], []));
    }

    [Fact]
    public void DuplicateOutputLotIsRejected()
    {
        var secondOutput = CreateOutput(
            id: Guid.Parse("d5596b41-b987-43df-ae37-ae96e4cd518d"));

        Assert.Throws<TraceabilityDomainException>(() =>
            CreateEvent([], [CreateOutput(), secondOutput]));
    }

    [Fact]
    public void SameLotOnBothSidesIsAccepted()
    {
        var input = CreateInput(lotId: InputLotId);
        var output = CreateOutput(lotId: InputLotId);

        var traceabilityEvent = CreateEvent([input], [output]);

        Assert.Equal(InputLotId, Assert.Single(traceabilityEvent.Inputs).LotId);
        Assert.Equal(InputLotId, Assert.Single(traceabilityEvent.Outputs).LotId);
    }

    [Fact]
    public void EmptyEventIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => CreateEvent([CreateInput()], [], id: Guid.Empty));

        Assert.Contains("event id", exception.Message);
    }

    [Fact]
    public void EmptyEventTypeIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => CreateEvent([CreateInput()], [], eventTypeId: Guid.Empty));

        Assert.Contains("event type id", exception.Message);
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => CreateEvent([CreateInput()], [], organizationId: Guid.Empty));

        Assert.Contains("organization id", exception.Message);
    }

    [Fact]
    public void EmptyLocationIdIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => CreateEvent([CreateInput()], [], locationId: Guid.Empty));

        Assert.Contains("location id", exception.Message);
    }

    [Fact]
    public void EmptyCreatedByIsRejected()
    {
        var exception = Assert.Throws<TraceabilityDomainException>(
            () => CreateEvent([CreateInput()], [], createdBy: Guid.Empty));

        Assert.Contains("created by", exception.Message);
    }

    [Fact]
    public void NullOptionalTextRemainsNull()
    {
        var traceabilityEvent = CreateEvent(
            [CreateInput()],
            [],
            externalReference: null,
            description: null);

        Assert.Null(traceabilityEvent.ExternalReference);
        Assert.Null(traceabilityEvent.Description);
    }

    [Fact]
    public void OptionalTextIsTrimmed()
    {
        var traceabilityEvent = CreateEvent(
            [CreateInput()],
            [],
            externalReference: "  EXT-002  ",
            description: "  Bottling completed  ");

        Assert.Equal("EXT-002", traceabilityEvent.ExternalReference);
        Assert.Equal("Bottling completed", traceabilityEvent.Description);
    }

    [Fact]
    public void WhitespaceOnlyExternalReferenceIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent(
            [CreateInput()],
            [],
            externalReference: "   "));
    }

    [Fact]
    public void WhitespaceOnlyDescriptionIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => CreateEvent(
            [CreateInput()],
            [],
            description: "   "));
    }

    [Fact]
    public void OptionalTextAtMaximumLengthsIsAccepted()
    {
        var externalReference = new string('R', TraceabilityEvent.MaximumExternalReferenceLength);
        var description = new string('D', TraceabilityEvent.MaximumDescriptionLength);

        var traceabilityEvent = CreateEvent(
            [CreateInput()],
            [],
            externalReference: externalReference,
            description: description);

        Assert.Equal(externalReference, traceabilityEvent.ExternalReference);
        Assert.Equal(description, traceabilityEvent.Description);
    }

    [Fact]
    public void ExternalReferenceOverMaximumLengthIsRejectedAfterTrimming()
    {
        var externalReference =
            $"  {new string('R', TraceabilityEvent.MaximumExternalReferenceLength + 1)}  ";

        Assert.Throws<TraceabilityDomainException>(() => CreateEvent(
            [CreateInput()],
            [],
            externalReference: externalReference));
    }

    [Fact]
    public void DescriptionOverMaximumLengthIsRejectedAfterTrimming()
    {
        var description =
            $"  {new string('D', TraceabilityEvent.MaximumDescriptionLength + 1)}  ";

        Assert.Throws<TraceabilityDomainException>(() => CreateEvent(
            [CreateInput()],
            [],
            description: description));
    }

    [Fact]
    public void InputAndOutputListsAreDefensivelyCopied()
    {
        var inputs = new List<EventInput> { CreateInput() };
        var outputs = new List<EventOutput> { CreateOutput() };
        var traceabilityEvent = CreateEvent(inputs, outputs);

        inputs.Add(CreateInput(
            id: Guid.Parse("ed9ce71f-ce5d-421d-8644-4c7bd75c7874"),
            lotId: Guid.Parse("f037dc54-c3c8-432e-8b1b-c39579d58833")));
        outputs.Add(CreateOutput(
            id: Guid.Parse("8adb73c7-85c8-4f2d-a35f-0f8d1e0d2a39"),
            lotId: Guid.Parse("7754d4f6-f507-458d-a12f-7777cc27bd10")));

        Assert.Equal((1, 1), (traceabilityEvent.Inputs.Count, traceabilityEvent.Outputs.Count));
    }

    [Fact]
    public void InputAndOutputListsCannotBeModifiedByCallers()
    {
        var traceabilityEvent = CreateEvent([CreateInput()], [CreateOutput()]);

        Assert.Null(traceabilityEvent.Inputs as List<EventInput>);
        var inputs = Assert.IsAssignableFrom<ICollection<EventInput>>(traceabilityEvent.Inputs);
        Assert.Throws<NotSupportedException>(() => inputs.Add(CreateInput(
            id: Guid.Parse("ed9ce71f-ce5d-421d-8644-4c7bd75c7874"),
            lotId: Guid.Parse("f037dc54-c3c8-432e-8b1b-c39579d58833"))));

        Assert.Null(traceabilityEvent.Outputs as List<EventOutput>);
        var outputs = Assert.IsAssignableFrom<ICollection<EventOutput>>(traceabilityEvent.Outputs);
        Assert.Throws<NotSupportedException>(() => outputs.Add(CreateOutput(
            id: Guid.Parse("8adb73c7-85c8-4f2d-a35f-0f8d1e0d2a39"),
            lotId: Guid.Parse("7754d4f6-f507-458d-a12f-7777cc27bd10"))));
    }

    private static TraceabilityEvent CreateEvent(
        IReadOnlyList<EventInput>? inputs,
        IReadOnlyList<EventOutput>? outputs,
        Guid? id = null,
        Guid? eventTypeId = null,
        Guid? organizationId = null,
        Guid? locationId = null,
        string? externalReference = "EXT-001",
        string? description = "Pressing",
        Guid? createdBy = null)
    {
        return TraceabilityEvent.Create(
            id ?? EventId,
            eventTypeId ?? EventTypeId,
            organizationId ?? OrganizationId,
            locationId ?? LocationId,
            OccurredAt,
            externalReference,
            description,
            createdBy ?? CreatedBy,
            CreatedAt,
            inputs,
            outputs);
    }

    private static EventInput CreateInput(Guid? id = null, Guid? lotId = null)
    {
        return EventInput.Create(
            id ?? InputId,
            lotId ?? InputLotId,
            100m,
            UnitId);
    }

    private static EventOutput CreateOutput(Guid? id = null, Guid? lotId = null)
    {
        return EventOutput.Create(
            id ?? OutputId,
            lotId ?? OutputLotId,
            80m,
            UnitId);
    }
}
