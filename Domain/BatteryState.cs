namespace RedmiBudsMonitor;

internal sealed class BatteryState
{
    private readonly Lock _lock = new();

    private byte _lastKnownCase = BatterySnapshot.Unavailable;
    private BatteryEntry _left = BatteryEntry.Empty;
    private BatteryEntry _right = BatteryEntry.Empty;
    private BatteryEntry _case = BatteryEntry.Empty;

    public void Update(BudsAdvertisement buds)
    {
        lock (_lock)
        {
            if (buds.HasCase) _lastKnownCase = buds.Case.Battery;

            var leftPct = buds.HasLeft ? buds.Left.Battery : BatterySnapshot.Unavailable;
            var rightPct = buds.HasRight ? buds.Right.Battery : BatterySnapshot.Unavailable;
            var casePct = _lastKnownCase.IsValid ? _lastKnownCase : BatterySnapshot.Unavailable;

            _left = new BatteryEntry(leftPct, leftPct.ToLabel(IsCharging(leftPct, casePct, buds.Left.InCase)));
            _right = new BatteryEntry(rightPct, rightPct.ToLabel(IsCharging(rightPct, casePct, buds.Right.InCase)));
            _case = new BatteryEntry(casePct, casePct.ToLabel(buds.Case.Charging));
        }
    }

    public BatterySnapshot Snapshot()
    {
        lock (_lock) return new BatterySnapshot(_left, _right, _case);
    }

    private static bool IsCharging(byte pct, byte casePct, bool inCase)
        => inCase && pct < 100 && casePct > 0;
}