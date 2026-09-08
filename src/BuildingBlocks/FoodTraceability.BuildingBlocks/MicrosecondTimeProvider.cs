namespace FoodTraceability.BuildingBlocks;

public sealed class MicrosecondTimeProvider : TimeProvider
{
    private readonly TimeProvider _inner;

    public MicrosecondTimeProvider(TimeProvider inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public static new MicrosecondTimeProvider System { get; } = new(TimeProvider.System);

    public override TimeZoneInfo LocalTimeZone => _inner.LocalTimeZone;

    public override long TimestampFrequency => _inner.TimestampFrequency;

    // D-36: PostgreSQL persists microseconds, so truncate before persistence to preserve exact values.
    public override DateTimeOffset GetUtcNow()
    {
        var now = _inner.GetUtcNow();
        var ticks = now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerMicrosecond;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public override long GetTimestamp() => _inner.GetTimestamp();

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) =>
        _inner.CreateTimer(callback, state, dueTime, period);
}
