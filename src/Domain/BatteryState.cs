namespace TrayBatt;

internal sealed class BatteryState
{
    private readonly Lock _lock = new();

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
            _lastLeftInCase = buds.Left.InCase;
            _lastRightInCase = buds.Right.InCase;

            if (buds.HasLeft && (!buds.Left.InCase || buds.LidOpen))
                _lastLeftPct = buds.Left.Battery;

            if (buds.HasRight && (!buds.Right.InCase || buds.LidOpen))
                _lastRightPct = buds.Right.Battery;

            if (buds.HasCase)
            {
                _lastCasePct = buds.Case.Battery;
                _lastCaseCharging = buds.Case.Charging;
            }

            RebuildEntries();
        }
    }

    public void ApplyConnectedBattery(byte pct)
    {
        if (!pct.IsValid()) return;
        lock (_lock)
        {
            var changed = false;

            if (!_lastLeftInCase && _lastLeftPct != pct)
            {
                _lastLeftPct = pct;
                changed = true;
            }

            if (!_lastRightInCase && _lastRightPct != pct)
            {
                _lastRightPct = pct;
                changed = true;
            }

            if (changed) RebuildEntries();
        }
    }

    public BatterySnapshot Snapshot()
    {
        lock (_lock) return new BatterySnapshot(_left, _right, _case);
    }

    private void RebuildEntries()
    {
        var leftPct = _lastLeftPct.IsValid() ? _lastLeftPct : BatterySnapshot.Unavailable;
        var rightPct = _lastRightPct.IsValid() ? _lastRightPct : BatterySnapshot.Unavailable;
        var casePct = _lastCasePct.IsValid() ? _lastCasePct : BatterySnapshot.Unavailable;

        _left = new BatteryEntry(leftPct, leftPct.ToLabel(IsCharging(leftPct, casePct, _lastLeftInCase)), _lastLeftInCase);
        _right = new BatteryEntry(rightPct, rightPct.ToLabel(IsCharging(rightPct, casePct, _lastRightInCase)), _lastRightInCase);
        _case = new BatteryEntry(casePct, casePct.ToLabel(_lastCaseCharging));

        if (leftPct.IsValid()) _baselineLeft = leftPct;
        if (rightPct.IsValid()) _baselineRight = rightPct;
        PersistIfChanged();
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
}
