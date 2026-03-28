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

    public void Start() => _scanner.Start();

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
            Icon             = TrayIconRenderer.Render(BatterySnapshot.Empty),
            Visible          = false,
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
        if (!connected)
            _ctx.Post(_ => _tray.Visible = false, null);
    }

    private void OnRefreshTick(object? _) => RefreshUi();

    private void RefreshUi()
    {
        var snapshot = _state.Snapshot();
        if (!snapshot.MinPercent.IsValid()) return;

        Icon? icon;
        try { icon = TrayIconRenderer.Render(snapshot); }
        catch { return; }

        _ctx.Post(_ =>
        {
            var old = _tray.Icon;
            _tray.Icon = icon;
            old?.Dispose();
            if (!_tray.Visible) _tray.Visible = true;
            _popup.UpdateData(snapshot);
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
