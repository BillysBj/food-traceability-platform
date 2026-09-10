using FoodTraceability.BuildingBlocks;

namespace FoodTraceability.UnitTests;

public sealed class TimestampPrecisionTests
{
    private static readonly DateTimeOffset UtcBase =
        new(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TruncatesTicksBelowOneMicrosecond()
    {
        var value = UtcBase.AddTicks(1_234_567);

        var result = TimestampPrecision.TruncateToMicroseconds(value);

        Assert.Equal(UtcBase.AddTicks(1_234_560).UtcTicks, result.UtcTicks);
    }

    [Fact]
    public void PreservesWholeMicroseconds()
    {
        var value = UtcBase.AddTicks(1_234_560);

        var result = TimestampPrecision.TruncateToMicroseconds(value);

        Assert.True(value.EqualsExact(result));
    }

    [Fact]
    public void PreservesOffset()
    {
        var value = UtcBase.ToOffset(TimeSpan.FromHours(2)).AddTicks(1_234_567);

        var result = TimestampPrecision.TruncateToMicroseconds(value);

        Assert.Equal(TimeSpan.FromHours(2), result.Offset);
        Assert.Equal(value.Ticks - 7, result.Ticks);
        Assert.Equal(value.UtcTicks - 7, result.UtcTicks);
    }

}
