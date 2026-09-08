using FoodTraceability.BuildingBlocks;

namespace FoodTraceability.UnitTests;

public sealed class MicrosecondTimeProviderTests
{
    private static readonly DateTimeOffset UtcBase =
        new(2026, 9, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetUtcNowTruncatesTicksBelowOneMicrosecond()
    {
        var now = UtcBase.AddTicks(1_234_567);
        var provider = new MicrosecondTimeProvider(new ControlledTimeProvider(now));

        var result = provider.GetUtcNow();

        Assert.Equal(UtcBase.AddTicks(1_234_560), result);
    }

    [Fact]
    public void GetUtcNowPreservesWholeMicroseconds()
    {
        var now = UtcBase.AddTicks(1_234_560);
        var provider = new MicrosecondTimeProvider(new ControlledTimeProvider(now));

        var result = provider.GetUtcNow();

        Assert.Equal(now, result);
    }

    [Fact]
    public void GetUtcNowAlwaysReturnsZeroOffset()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            8,
            3,
            0,
            0,
            TimeSpan.FromHours(2)).AddTicks(7);
        var provider = new MicrosecondTimeProvider(new ControlledTimeProvider(now));

        var result = provider.GetUtcNow();

        Assert.Equal(TimeSpan.Zero, result.Offset);
        Assert.Equal(UtcBase, result);
    }

    [Fact]
    public void TimestampAndTimeZoneMembersDelegateToInnerProvider()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Controlled/Test",
            TimeSpan.FromHours(3),
            "Controlled Test",
            "Controlled Test");
        var inner = new ControlledTimeProvider(
            UtcBase,
            timeZone,
            timestamp: 123_456_789,
            timestampFrequency: 10_000_000);
        var provider = new MicrosecondTimeProvider(inner);

        Assert.Same(timeZone, provider.LocalTimeZone);
        Assert.Equal(123_456_789, provider.GetTimestamp());
        Assert.Equal(10_000_000, provider.TimestampFrequency);
    }

    private sealed class ControlledTimeProvider(
        DateTimeOffset utcNow,
        TimeZoneInfo? localTimeZone = null,
        long timestamp = 0,
        long timestampFrequency = TimeSpan.TicksPerSecond) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            localTimeZone ?? TimeZoneInfo.Utc;

        public override long TimestampFrequency { get; } = timestampFrequency;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;
    }
}
