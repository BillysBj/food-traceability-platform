using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EventTypeClassificationTests
{
    [Theory]
    [InlineData(EventTypeClassification.Traceability, "TRACEABILITY")]
    [InlineData(EventTypeClassification.TraceabilityAwaitingLogistics, "TRACEABILITY_AWAITING_LOGISTICS")]
    [InlineData(EventTypeClassification.Logistics, "LOGISTICS")]
    [InlineData(EventTypeClassification.Quality, "QUALITY")]
    [InlineData(EventTypeClassification.Deferred, "DEFERRED")]
    public void ClassificationConvertsToExpectedCode(EventTypeClassification classification, string code)
    {
        Assert.Equal(code, EventTypeClassificationCodes.ToCode(classification));
    }

    [Theory]
    [InlineData("TRACEABILITY", EventTypeClassification.Traceability)]
    [InlineData("TRACEABILITY_AWAITING_LOGISTICS", EventTypeClassification.TraceabilityAwaitingLogistics)]
    [InlineData("LOGISTICS", EventTypeClassification.Logistics)]
    [InlineData("QUALITY", EventTypeClassification.Quality)]
    [InlineData("DEFERRED", EventTypeClassification.Deferred)]
    public void CodeConvertsToExpectedClassification(string code, EventTypeClassification classification)
    {
        Assert.Equal(classification, EventTypeClassificationCodes.FromCode(code));
    }

    [Fact]
    public void UnknownClassificationCodeIsRejected()
    {
        Assert.Throws<TraceabilityDomainException>(() => EventTypeClassificationCodes.FromCode("UNKNOWN"));
    }

    [Fact]
    public void InvalidClassificationCannotBeConvertedToCode()
    {
        Assert.Throws<TraceabilityDomainException>(() =>
            EventTypeClassificationCodes.ToCode((EventTypeClassification)999));
    }

    [Fact]
    public void InvalidClassificationCannotBeUsedToCreateEventType()
    {
        Assert.Throws<TraceabilityDomainException>(() => EventType.Create(
            Guid.NewGuid(),
            EventTypeCode.Create("PRESS"),
            (EventTypeClassification)999,
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)));
    }
}
