using Timer = System.Threading.Timer;

namespace RedmiBudsMonitor;

internal sealed class TrayApp : IDisposable
{
    private readonly BleScanner _scanner;
    private readonly BluetoothConnectionWatcher _btWatcher;
    private readonly BatteryState _state;
    private readonly NotifyIcon _tray;
    private readonly BatteryPopup _popup;
    private readonly ContextMenuStrip _menu;
    private readonly SynchronizationContext _ctx;
    private readonly Timer _refreshTimer;

    private volatile bool _connected;

    private const string DeviceName = "Redmi Buds";
    private const string AppTitle = "Redmi Buds 5";

    public TrayApp()
    {
        _ctx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _state = new BatteryState();
        _menu = BuildMenu();
        _popup = new BatteryPopup();
        _tray = BuildTrayIcon();

        _refreshTimer = new Timer(OnRefreshTick, null, 10_000, 10_000);

        _btWatcher = new BluetoothConnectionWatcher(DeviceName);
        _btWatcher.ConnectionChanged += OnConnectionChanged;
        _btWatcher.Start();

        _scanner = new BleScanner();
        _scanner.OnBudsData += buds => _state.Update(buds);
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
            Icon = TrayIconRenderer.Render(BatterySnapshot.Empty),
            Visible = false,
            Text = AppTitle,
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
        _connected = connected;
        if (connected) RefreshUi();
        else _ctx.Post(_ => _tray.Visible = false, null);
    }

    private void OnRefreshTick(object? _) => RefreshUi();

    private void RefreshUi()
    {
        if (!_connected) return;

        var snapshot = _state.Snapshot();
        var icon = TrayIconRenderer.Render(snapshot);

        _ctx.Post(_ =>
        {
            if (!_tray.Visible) _tray.Visible = true;
            var old = _tray.Icon;
            _tray.Icon = icon;
            old?.Dispose();
            _popup.UpdateData(snapshot);
        }, null);
    }

    public void Dispose()
    {
        _btWatcher.Dispose();
        _refreshTimer.Dispose();
        _scanner.Dispose();
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
    }
}