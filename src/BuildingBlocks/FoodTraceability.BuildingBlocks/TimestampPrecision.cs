namespace FoodTraceability.BuildingBlocks;

public static class TimestampPrecision
{
    // D-36: Match PostgreSQL precision without changing the supplied offset.
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMicrosecond, value.Offset);
}
