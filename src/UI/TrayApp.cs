using Timer = System.Threading.Timer;

namespace RedmiBudsMonitor;

internal sealed class TrayApp : IDisposable
{
    private const string DeviceName = "Redmi Buds";
    private const string AppTitle   = "Redmi Buds 5";
    private const int    RefreshMs  = 10_000;

    private readonly BleScanner                 _scanner;
    private readonly BluetoothConnectionWatcher _btWatcher;
    private readonly BatteryState               _state;
    private readonly NotifyIcon                 _tray;
    private readonly BatteryPopup               _popup;
    private readonly ContextMenuStrip           _menu;
    private readonly SynchronizationContext     _ctx;
    private readonly Timer                      _refreshTimer;

    private volatile IReadOnlyList<DeviceBattery> _devices = [];
    private volatile bool _budsConnected;
    private int _deviceQueryBusy;

    public TrayApp()
    {
        _ctx   = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _state = new BatteryState();
        _menu  = BuildMenu();
        _popup = new BatteryPopup();
        _tray  = BuildTrayIcon();

        _refreshTimer = new Timer(OnRefreshTick, null, RefreshMs, RefreshMs);

        _btWatcher = new BluetoothConnectionWatcher(DeviceName);
        _btWatcher.ConnectionChanged += OnConnectionChanged;
        _btWatcher.Start();

        _scanner = new BleScanner();
        _scanner.OnBudsData += buds =>
        {
            _state.Update(buds);
            RefreshUi();
        };
    }

    public void Start()
    {
        _scanner.Start();
        _ = RefreshDevicesAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Sair", null, (_, _) => Application.Exit());
        return menu;
    }

    private NotifyIcon BuildTrayIcon()
    {
        var tray = new NotifyIcon
        {
            Icon             = TrayIconRenderer.Render(BatterySnapshot.Unavailable, false),
            Visible          = true,
            Text             = AppTitle,
            ContextMenuStrip = _menu,
        };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _popup.ToggleNearTray();
        };
        return tray;
    }

    private void OnConnectionChanged(bool connected)
    {
        _budsConnected = connected;
        if (!connected) _state.Reset();
        RefreshUi();
    }

    private void OnRefreshTick(object? _) => _ = RefreshDevicesAsync();

    private async Task RefreshDevicesAsync()
    {
        if (Interlocked.Exchange(ref _deviceQueryBusy, 1) == 1) return;
        try
        {
            var all = await BluetoothBatteryEnumerator.QueryConnectedAsync();

            var others = new List<DeviceBattery>(all.Count);
            var budsPct = BatterySnapshot.Unavailable;
            foreach (var device in all)
            {
                if (device.Name.StartsWith(DeviceName, StringComparison.OrdinalIgnoreCase))
                    budsPct = device.Pct;
                else
                    others.Add(device);
            }

            if (budsPct.IsValid()) _state.ApplyHeadsetFallback(budsPct);
            _devices = others;
        }
        catch
        {
        }
        finally
        {
            Interlocked.Exchange(ref _deviceQueryBusy, 0);
        }
        RefreshUi();
    }

    private void RefreshUi()
    {
        var snapshot      = _state.Snapshot();
        var devices       = _devices;
        var budsConnected = _budsConnected;

        var overallMin = budsConnected ? snapshot.MinPercent : BatterySnapshot.Unavailable;
        foreach (var device in devices)
            if (device.Pct.IsValid() && device.Pct < overallMin) overallMin = device.Pct;

        Icon? icon;
        try { icon = TrayIconRenderer.Render(overallMin, budsConnected); }
        catch { icon = null; }

        _ctx.Post(_ =>
        {
            if (icon is not null)
            {
                var old = _tray.Icon;
                _tray.Icon = icon;
                old?.Dispose();
            }
            if (!_tray.Visible) _tray.Visible = true;
            _popup.UpdateData(snapshot, devices);
        }, null);
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _btWatcher.Dispose();
        _scanner.Dispose();
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Icon?.Dispose();
        _tray.Dispose();
        _menu.Dispose();
    }
}
