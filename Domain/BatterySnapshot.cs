namespace RedmiBudsMonitor;

internal readonly record struct BatterySnapshot(BatteryEntry Left, BatteryEntry Right, BatteryEntry Case)
{
    public const byte Unavailable = 0xFF;

    public static BatterySnapshot Empty { get; } =
        new(BatteryEntry.Empty, BatteryEntry.Empty, BatteryEntry.Empty);

    public byte MinPercent
    {
        get
        {
            var min = Unavailable;
            if (Left.Pct.IsValid) min = Math.Min(min, Left.Pct);
            if (Right.Pct.IsValid) min = Math.Min(min, Right.Pct);
            if (Case.Pct.IsValid) min = Math.Min(min, Case.Pct);
            return min;
        }
    }
}