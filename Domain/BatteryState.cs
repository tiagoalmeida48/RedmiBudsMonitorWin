namespace RedmiBudsMonitor;

internal sealed class BatteryState
{
    private const long AdvertisementTtlMs = 30_000;

    private readonly Lock _lock = new();

    private long? _lastAdvertisementAt;
    private byte _lastLeftPct = BatterySnapshot.Unavailable;
    private byte _lastRightPct = BatterySnapshot.Unavailable;
    private byte _lastCasePct = BatterySnapshot.Unavailable;
    private bool _lastLeftInCase;
    private bool _lastRightInCase;
    private bool _lastCaseCharging;

    private byte _baselineLeft = BatterySnapshot.Unavailable;
    private byte _baselineRight = BatterySnapshot.Unavailable;
    private BatteryStore.StoredLevels? _persisted;

    private BatteryEntry _left = BatteryEntry.Empty;
    private BatteryEntry _right = BatteryEntry.Empty;
    private BatteryEntry _case = BatteryEntry.Empty;

    public BatteryState()
    {
        var stored = BatteryStore.TryLoad();
        if (stored is null) return;

        _persisted = stored;
        _baselineLeft = stored.Left;
        _baselineRight = stored.Right;
        if (stored.Case.IsValid())
        {
            _lastCasePct = stored.Case;
            _case = new BatteryEntry(stored.Case, stored.Case.ToLabel(false));
        }
    }

    public void Update(BudsAdvertisement buds)
    {
        lock (_lock)
        {
            _lastAdvertisementAt = Environment.TickCount64;

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

            if (leftPct.IsValid()) _baselineLeft = leftPct;
            if (rightPct.IsValid()) _baselineRight = rightPct;
            PersistIfChanged();
        }
    }

    public BatterySnapshot Snapshot()
    {
        lock (_lock) return new BatterySnapshot(_left, _right, _case);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _lastAdvertisementAt = null;
            _lastLeftPct = _lastRightPct = BatterySnapshot.Unavailable;
            _lastLeftInCase = _lastRightInCase = false;
            _lastCaseCharging = false;
            _left = _right = BatteryEntry.Empty;
            _case = new BatteryEntry(_lastCasePct, _lastCasePct.ToLabel(false));
        }
    }

    public void ApplyHeadsetFallback(byte pct)
    {
        if (!pct.IsValid()) return;
        lock (_lock)
        {
            if (_lastAdvertisementAt is { } at && Environment.TickCount64 - at < AdvertisementTtlMs) return;

            var left = _baselineLeft.IsValid() ? _baselineLeft : pct;
            var right = _baselineRight.IsValid() ? _baselineRight : pct;
            var delta = pct - Math.Min(left, right);
            var newLeft = ClampPct(left + delta);
            var newRight = ClampPct(right + delta);

            _baselineLeft = _lastLeftPct = newLeft;
            _baselineRight = _lastRightPct = newRight;
            _lastLeftInCase = _lastRightInCase = false;
            _left = new BatteryEntry(newLeft, newLeft.ToLabel(false));
            _right = new BatteryEntry(newRight, newRight.ToLabel(false));

            PersistIfChanged();
        }
    }

    private void PersistIfChanged()
    {
        var current = new BatteryStore.StoredLevels(_baselineLeft, _baselineRight, _lastCasePct);
        if (current == _persisted) return;
        _persisted = current;
        BatteryStore.Save(current);
    }

    private static bool IsCharging(byte pct, byte casePct, bool inCase)
        => inCase && pct < 100 && casePct > 0;

    private static byte ClampPct(int value) => (byte)Math.Clamp(value, 0, 100);
}
