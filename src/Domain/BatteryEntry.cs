namespace TrayBatt;

internal readonly record struct BatteryEntry(byte Pct, string Label, bool InCase = false)
{
    internal static BatteryEntry Empty { get; } = new(BatterySnapshot.Unavailable, "--");
}