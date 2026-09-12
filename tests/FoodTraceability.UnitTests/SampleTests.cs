using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class SampleTests
{
    private static readonly Guid SampleId = Guid.Parse("f5d275f8-9976-4d0a-a7be-982aec81f627");
    private static readonly Guid OrganizationId = Guid.Parse("7fa53a95-97be-47bc-a458-e1f1119e5ab5");
    private static readonly Guid LotId = Guid.Parse("38494848-e374-44ba-971d-2a19b55f127b");
    private static readonly Guid LocationId = Guid.Parse("05dba94a-7cea-42e5-ae5c-b11677218283");
    private static readonly Guid EventId = Guid.Parse("e62f0946-596b-47ae-bc53-4fc7fcc46a6b");
    private static readonly DateTimeOffset TakenAt = new(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidSampleIsCreatedPendingWithTrimmedNumberAndOriginalCasing()
    {
        var sample = Create(sampleNumber: "  Sample-123  ");

        Assert.Equal(SampleId, sample.Id);
        Assert.Equal(OrganizationId, sample.OrganizationId);
        Assert.Equal(LotId, sample.LotId);
        Assert.Equal(LocationId, sample.LocationId);
        Assert.Equal(EventId, sample.TraceabilityEventId);
        Assert.Equal("Sample-123", sample.SampleNumber);
        Assert.Equal(TakenAt, sample.TakenAt);
        Assert.Equal(SampleStatus.Pending, sample.Status);
        Assert.Equal(CreatedAt, sample.CreatedAt);
    }

    [Fact]
    public void SampleStatusHasExactlyTheThreeDecidedValues()
    {
        Assert.Equal([SampleStatus.Pending, SampleStatus.Pass, SampleStatus.Fail], Enum.GetValues<SampleStatus>());
        Assert.Equal(["Pending", "Pass", "Fail"], Enum.GetNames<SampleStatus>());
    }

    [Fact]
    public void EmptySampleIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(id: Guid.Empty));
    }

    [Fact]
    public void EmptyOrganizationIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(organizationId: Guid.Empty));
    }

    [Fact]
    public void EmptyLotIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(lotId: Guid.Empty));
    }

    [Fact]
    public void EmptyLocationIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(locationId: Guid.Empty));
    }

    [Fact]
    public void EmptyTraceabilityEventIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(traceabilityEventId: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSampleNumberIsRejected(string? sampleNumber)
    {
        Assert.Throws<QualityDomainException>(() => Create(sampleNumber: sampleNumber));
    }

    [Fact]
    public void SampleNumberAtMaximumLengthIsAccepted()
    {
        var number = new string('A', Sample.MaximumSampleNumberLength);

        Assert.Equal(number, Create(sampleNumber: number).SampleNumber);
    }

    [Fact]
    public void SampleNumberOverMaximumLengthIsRejectedAfterTrimming()
    {
        var number = $"  {new string('A', Sample.MaximumSampleNumberLength + 1)}  ";

        Assert.Throws<QualityDomainException>(() => Create(sampleNumber: number));
    }

    [Fact]
    public void TimestampsRetainSubMicrosecondPrecisionAndSuppliedOffsets()
    {
        var takenAt = TakenAt.ToOffset(TimeSpan.FromHours(2)).AddTicks(17);
        var createdAt = CreatedAt.ToOffset(TimeSpan.FromHours(-3)).AddTicks(29);

        var sample = Create(takenAt: takenAt, createdAt: createdAt);

        Assert.True(takenAt.EqualsExact(sample.TakenAt));
        Assert.True(createdAt.EqualsExact(sample.CreatedAt));
    }

    private static Sample Create(
        Guid? id = null,
        Guid? organizationId = null,
        Guid? lotId = null,
        Guid? locationId = null,
        Guid? traceabilityEventId = null,
        string? sampleNumber = "Sample-123",
        DateTimeOffset? takenAt = null,
        DateTimeOffset? createdAt = null) =>
        Sample.Create(
            id ?? SampleId,
            organizationId ?? OrganizationId,
            lotId ?? LotId,
            locationId ?? LocationId,
            traceabilityEventId ?? EventId,
            sampleNumber,
            takenAt ?? TakenAt,
            createdAt ?? CreatedAt);
}
