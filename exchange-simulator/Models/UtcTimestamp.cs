namespace exchange_simulator.Models;


internal static class UtcTimestamp
{
    private const long TicksPerMicrosecond = 10;

    internal static DateTimeOffset Now()
    {
        var value = DateTimeOffset.UtcNow;
        return value.AddTicks(-(value.Ticks % TicksPerMicrosecond));
    }
}