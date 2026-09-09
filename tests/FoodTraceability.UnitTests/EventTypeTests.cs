using System.Globalization;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EventTypeTests
{
    private static readonly Guid EventTypeId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EventTypeCodeIsTrimmedAndUppercasedInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var code = EventTypeCode.Create("  quality_release  ");

            Assert.Equal("QUALITY_RELEASE", code.Value);
            Assert.Equal(code.Value, code.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingEventTypeCodeIsRejected(string? code)
    {
        Assert.Throws<TraceabilityDomainException>(() => EventTypeCode.Create(code));
    }

    [Fact]
    public void EventTypeCodeOverMaximumLengthIsRejectedAfterTrimming()
    {
        var code = $"  {new string('A', EventTypeCode.MaximumLength + 1)}  ";

        Assert.Throws<TraceabilityDomainException>(() => EventTypeCode.Create(code));
    }

    [Fact]
    public void EventTypeCodeWithDisallowedCharacterIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => EventTypeCode.Create("QUALITY-RELEASE"));
    }

    [Fact]
    public void EmptyEventTypeIdIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => EventType.Create(
            Guid.Empty,
            EventTypeCode.Create("PRESS"),
            EventTypeClassification.Traceability,
            CreatedAt));
    }

    [Fact]
    public void MissingEventTypeCodeIsRejectedByEventType()
    {
        Assert.Throws<TraceabilityDomainException>(() => EventType.Create(
            EventTypeId,
            null,
            EventTypeClassification.Traceability,
            CreatedAt));
    }

    [Fact]
    public void ValidEventTypeIsCreatedWithTheProvidedValues()
    {
        var code = EventTypeCode.Create("PRESS");

        var eventType = EventType.Create(EventTypeId, code, EventTypeClassification.Traceability, CreatedAt);

        Assert.Equal(EventTypeId, eventType.Id);
        Assert.Same(code, eventType.Code);
        Assert.Equal(EventTypeClassification.Traceability, eventType.Classification);
        Assert.Equal(CreatedAt, eventType.CreatedAt);
    }
}
