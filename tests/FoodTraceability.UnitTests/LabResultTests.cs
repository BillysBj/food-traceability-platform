using System.Globalization;
using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class LabResultTests
{
    private static readonly Guid ResultId = Guid.Parse("158bf10b-5d82-49fc-a9ae-3c97d493977a");
    private static readonly Guid SampleId = Guid.Parse("58d2b62b-d8ef-41b5-9a9d-9d01d758f78f");
    private static readonly Guid ParameterId = Guid.Parse("259976b2-53d0-4fa0-bba5-993976d025df");
    private static readonly DateTimeOffset MeasuredAt = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(LabResultAssessment.Pass)]
    [InlineData(LabResultAssessment.Fail)]
    public void ValidResultRetainsAllValuesAndTrimsMethod(LabResultAssessment assessment)
    {
        var result = Create(assessment: assessment, method: "  ISO 660  ");

        Assert.Equal(ResultId, result.Id);
        Assert.Equal(SampleId, result.SampleId);
        Assert.Equal(ParameterId, result.ParameterId);
        Assert.Equal(0.123456m, result.Value);
        Assert.Equal(assessment, result.Assessment);
        Assert.Equal("ISO 660", result.Method);
        Assert.Equal(MeasuredAt, result.MeasuredAt);
        Assert.Equal(CreatedAt, result.CreatedAt);
    }

    [Fact]
    public void AssessmentHasExactlyTheTwoDecidedValues()
    {
        Assert.Equal([LabResultAssessment.Pass, LabResultAssessment.Fail], Enum.GetValues<LabResultAssessment>());
        Assert.Equal(["Pass", "Fail"], Enum.GetNames<LabResultAssessment>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void UndefinedAssessmentIsRejected(int assessment)
    {
        Assert.Throws<QualityDomainException>(() => Create(assessment: (LabResultAssessment)assessment));
    }

    [Fact]
    public void EmptyResultIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(id: Guid.Empty));
    }

    [Fact]
    public void EmptySampleIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(sampleId: Guid.Empty));
    }

    [Fact]
    public void EmptyParameterIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(parameterId: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void MissingMethodIsRejected(string? method)
    {
        Assert.Throws<QualityDomainException>(() => Create(method: method));
    }

    [Fact]
    public void MethodAtMaximumLengthIsAccepted()
    {
        var method = new string('A', LabResult.MaximumMethodLength);

        Assert.Equal(method, Create(method: method).Method);
    }

    [Fact]
    public void MethodOverMaximumLengthIsRejectedAfterTrimming()
    {
        var method = $"  {new string('A', LabResult.MaximumMethodLength + 1)}  ";

        Assert.Throws<QualityDomainException>(() => Create(method: method));
    }

    [Theory]
    [InlineData("-26.123456")]
    [InlineData("0")]
    [InlineData("999999999999.999999")]
    [InlineData("-999999999999.999999")]
    [InlineData("0.1234560")]
    public void RepresentableValuesIncludingNegativeAndZeroAreAccepted(string input)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        Assert.Equal(value, Create(value: value).Value);
    }

    [Theory]
    [InlineData("0.1234561")]
    [InlineData("-0.1234561")]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    [InlineData("-1000000000000")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335")]
    public void ValuesRequiringRoundingOrExceedingPrecisionAreRejected(string input)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        Assert.Throws<QualityDomainException>(() => Create(value: value));
    }

    [Fact]
    public void TimestampsRetainSubMicrosecondPrecisionAndSuppliedOffsets()
    {
        var measuredAt = MeasuredAt.ToOffset(TimeSpan.FromHours(2)).AddTicks(17);
        var createdAt = CreatedAt.ToOffset(TimeSpan.FromHours(-3)).AddTicks(29);

        var result = Create(measuredAt: measuredAt, createdAt: createdAt);

        Assert.True(measuredAt.EqualsExact(result.MeasuredAt));
        Assert.True(createdAt.EqualsExact(result.CreatedAt));
    }

    private static LabResult Create(
        Guid? id = null,
        Guid? sampleId = null,
        Guid? parameterId = null,
        decimal value = 0.123456m,
        LabResultAssessment assessment = LabResultAssessment.Pass,
        string? method = "ISO 660",
        DateTimeOffset? measuredAt = null,
        DateTimeOffset? createdAt = null) =>
        LabResult.Create(
            id ?? ResultId, sampleId ?? SampleId, parameterId ?? ParameterId,
            value, assessment, method, measuredAt ?? MeasuredAt, createdAt ?? CreatedAt);
}
