namespace RedmiBudsMonitor;

internal readonly record struct BatteryEntry(byte Pct, string Label)
{
    internal static BatteryEntry Empty { get; } = new(BatterySnapshot.Unavailable, "--");
}