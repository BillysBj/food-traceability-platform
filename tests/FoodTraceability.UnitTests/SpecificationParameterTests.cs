using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class SpecificationParameterTests
{
    private static readonly Guid Id = Guid.Parse("7c94e6e2-d297-41c5-a9b3-e78c9c2d62e4");
    private static readonly Guid SpecificationId = Guid.Parse("5129eb12-648f-46db-80f4-690c651a2754");
    private static readonly Guid ParameterId = Guid.Parse("cac145df-bbc8-4d08-8bff-ae165b1bdd23");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidSpecificationParameterRetainsAllValues(bool required)
    {
        var parameter = Create(minimum: -1.123456m, maximum: 2.123456m, target: 0.123456m, required: required);

        Assert.Equal(Id, parameter.Id);
        Assert.Equal(SpecificationId, parameter.SpecificationId);
        Assert.Equal(ParameterId, parameter.ParameterId);
        Assert.Equal(-1.123456m, parameter.Minimum);
        Assert.Equal(2.123456m, parameter.Maximum);
        Assert.Equal(0.123456m, parameter.Target);
        Assert.Equal(required, parameter.Required);
        Assert.Equal(CreatedAt, parameter.CreatedAt);
    }

    [Fact]
    public void EmptyIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(id: Guid.Empty));
    }

    [Fact]
    public void EmptySpecificationIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(specificationId: Guid.Empty));
    }

    [Fact]
    public void EmptyParameterIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(parameterId: Guid.Empty));
    }

    [Fact]
    public void MinimumAboveMaximumIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Create(minimum: 2m, maximum: 1m));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void OptionalBoundsAndEqualBoundsAreAllowed(bool hasMinimum, bool hasMaximum)
    {
        decimal? minimum = hasMinimum ? 1m : null;
        decimal? maximum = hasMaximum ? 1m : null;

        var parameter = Create(minimum: minimum, maximum: maximum);

        Assert.Equal(minimum, parameter.Minimum);
        Assert.Equal(maximum, parameter.Maximum);
        Assert.Null(parameter.Target);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void TargetOutsideBoundsIsDeliberatelyAccepted(int target)
    {
        var parameter = Create(minimum: 0m, maximum: 2m, target: target);

        Assert.Equal((decimal)target, parameter.Target);
    }

    [Fact]
    public void TimestampRetainsSubMicrosecondPrecisionAndSuppliedOffset()
    {
        var createdAt = CreatedAt.ToOffset(TimeSpan.FromHours(-3)).AddTicks(29);

        var parameter = SpecificationParameter.Create(Id, SpecificationId, ParameterId, null, null, null, true, createdAt);

        Assert.True(createdAt.EqualsExact(parameter.CreatedAt));
    }

    private static SpecificationParameter Create(
        Guid? id = null, Guid? specificationId = null, Guid? parameterId = null,
        decimal? minimum = null, decimal? maximum = null, decimal? target = null, bool required = true) =>
        SpecificationParameter.Create(
            id ?? Id, specificationId ?? SpecificationId, parameterId ?? ParameterId,
            minimum, maximum, target, required, CreatedAt);
}
