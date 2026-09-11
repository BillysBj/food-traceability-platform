using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class ParameterTests
{
    [Fact]
    public void ValidParameterPreservesValuesAndTrimsTechnicalMethod()
    {
        var id = Guid.NewGuid();
        var code = ParameterCode.Create("INDICATOR_01");
        var unitId = Guid.NewGuid();

        var parameter = Parameter.Create(id, code, unitId, "  ISO 660  ");

        Assert.Equal(id, parameter.Id);
        Assert.Same(code, parameter.Code);
        Assert.Equal(unitId, parameter.UnitId);
        Assert.Equal("ISO 660", parameter.StandardMethod);
    }

    [Fact]
    public void ParameterWithoutUnitIsValid()
    {
        var parameter = Parameter.Create(
            Guid.NewGuid(), ParameterCode.Create("INDICATOR_01"), null, "ISO 660");

        Assert.Null(parameter.UnitId);
    }

    [Fact]
    public void EmptyParameterIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Parameter.Create(
            Guid.Empty, ParameterCode.Create("INDICATOR_01"), null, "ISO 660"));
    }

    [Fact]
    public void MissingCodeIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Parameter.Create(
            Guid.NewGuid(), null, null, "ISO 660"));
    }

    [Fact]
    public void EmptyUnitIdIsRejected()
    {
        Assert.Throws<QualityDomainException>(() => Parameter.Create(
            Guid.NewGuid(), ParameterCode.Create("INDICATOR_01"), Guid.Empty, "ISO 660"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingStandardMethodIsRejected(string? method)
    {
        Assert.Throws<QualityDomainException>(() => Parameter.Create(
            Guid.NewGuid(), ParameterCode.Create("INDICATOR_01"), null, method));
    }

    [Fact]
    public void StandardMethodAtMaximumLengthIsAcceptedAfterTrimming()
    {
        var method = new string('A', Parameter.MaximumStandardMethodLength);

        var parameter = Parameter.Create(
            Guid.NewGuid(), ParameterCode.Create("INDICATOR_01"), null, $"  {method}  ");

        Assert.Equal(method, parameter.StandardMethod);
    }

    [Fact]
    public void StandardMethodOverMaximumLengthIsRejectedAfterTrimming()
    {
        var method = new string('A', Parameter.MaximumStandardMethodLength + 1);

        Assert.Throws<QualityDomainException>(() => Parameter.Create(
            Guid.NewGuid(), ParameterCode.Create("INDICATOR_01"), null, $"  {method}  "));
    }
}
