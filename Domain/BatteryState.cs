namespace RedmiBudsMonitor;

internal sealed class BatteryState
{
    private readonly Lock _lock = new();

    private byte _lastLeftPct = BatterySnapshot.Unavailable;
    private byte _lastRightPct = BatterySnapshot.Unavailable;
    private byte _lastCasePct = BatterySnapshot.Unavailable;
    private bool _lastLeftInCase;
    private bool _lastRightInCase;
    private bool _lastCaseCharging;

    private BatteryEntry _left = BatteryEntry.Empty;
    private BatteryEntry _right = BatteryEntry.Empty;
    private BatteryEntry _case = BatteryEntry.Empty;

    public void Update(BudsAdvertisement buds)
    {
        lock (_lock)
        {
            if (buds.HasLeft)
            {
                _lastLeftPct = buds.Left.Battery;
                _lastLeftInCase = buds.Left.InCase;
            }

            if (buds.HasRight)
            {
                _lastRightPct = buds.Right.Battery;
                _lastRightInCase = buds.Right.InCase;
            }

            if (buds.HasCase)
            {
                _lastCasePct = buds.Case.Battery;
                _lastCaseCharging = buds.Case.Charging;
            }

            var leftPct = _lastLeftPct.IsValid() ? _lastLeftPct : BatterySnapshot.Unavailable;
            var rightPct = _lastRightPct.IsValid() ? _lastRightPct : BatterySnapshot.Unavailable;
            var casePct = _lastCasePct.IsValid() ? _lastCasePct : BatterySnapshot.Unavailable;

            _left = new BatteryEntry(leftPct, leftPct.ToLabel(IsCharging(leftPct, casePct, _lastLeftInCase)));
            _right = new BatteryEntry(rightPct, rightPct.ToLabel(IsCharging(rightPct, casePct, _lastRightInCase)));
            _case = new BatteryEntry(casePct, casePct.ToLabel(_lastCaseCharging));
        }
    }

    public BatterySnapshot Snapshot()
    {
        lock (_lock) return new BatterySnapshot(_left, _right, _case);
    }

    private static bool IsCharging(byte pct, byte casePct, bool inCase)
        => inCase && pct < 100 && casePct > 0;
}