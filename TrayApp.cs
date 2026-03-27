using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Timer = System.Threading.Timer;

namespace RedmiBudsMonitor;

internal sealed class TrayApp : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly BleScanner _scanner;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _tray;
    private readonly BatteryPopup _popup;
    private readonly SynchronizationContext _ctx;
    private readonly Timer _refreshTimer;
    private readonly DeviceWatcher _btWatcher;

    private readonly Lock _dataLock = new();
    private byte _lastCase = 0xFF;
    private string _leftStr = "--";
    private string _caseStr = "--";
    private string _rightStr = "--";
    private byte _leftPct = 0xFF;
    private byte _casePct = 0xFF;
    private byte _rightPct = 0xFF;

    private volatile bool _btConnected = false;
    private volatile string? _budsDeviceId;

    private const string BudsName = "Redmi Buds";

    public TrayApp()
    {
        _ctx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Sair", null, (_, _) => Application.Exit());

        _popup = new BatteryPopup();

        _tray = new NotifyIcon
        {
            Icon = RenderTrayIcon(0xFF, 0xFF, 0xFF),
            Visible = false,
            Text = "Redmi Buds 5",
            ContextMenuStrip = _menu,
        };

        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _popup.ToggleNearTray();
        };

        _refreshTimer = new Timer(OnRefreshTick, null, 10_000, 10_000);

        var aqs = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        _btWatcher = DeviceInformation.CreateWatcher(aqs, ["System.Devices.Aep.IsConnected"]);
        _btWatcher.Added += OnBtAdded;
        _btWatcher.Updated += OnBtUpdated;
        _btWatcher.Removed += OnBtRemoved;
        _btWatcher.Start();

        _scanner = new BleScanner();
        _scanner.OnBudsData += OnData;
    }

    public void Start() => _scanner.Start();

    private void OnData(BudsAdvertisement buds)
    {
        byte caseDisplay;
        lock (_dataLock)
        {
            if (buds.HasCase) _lastCase = buds.BatteryCase;
            caseDisplay = _lastCase;
        }

        var leftPct = buds.HasLeft ? buds.BatteryLeft : (byte)0xFF;
        var rightPct = buds.HasRight ? buds.BatteryRight : (byte)0xFF;
        var casePct = caseDisplay <= 100 ? caseDisplay : (byte)0xFF;

        var leftCharging = buds.IsLeftInCase && leftPct < 100 && casePct > 0;
        var rightCharging = buds.IsRightInCase && rightPct < 100 && casePct > 0;

        lock (_dataLock)
        {
            _leftStr = FormatPct(leftPct, leftCharging);
            _caseStr = FormatPct(casePct, buds.IsCaseCharging);
            _rightStr = FormatPct(rightPct, rightCharging);
            _leftPct = leftPct;
            _casePct = casePct;
            _rightPct = rightPct;
        }
    }

    private void OnBtAdded(DeviceWatcher _, DeviceInformation info)
    {
        if (!info.Name.Contains(BudsName, StringComparison.OrdinalIgnoreCase)) return;
        _budsDeviceId = info.Id;
        var connected = info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var v) && v is true;
        SetConnected(connected);
    }

    private void OnBtUpdated(DeviceWatcher _, DeviceInformationUpdate update)
    {
        if (update.Id != _budsDeviceId) return;
        if (!update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var v)) return;
        SetConnected(v is true);
    }

    private void OnBtRemoved(DeviceWatcher _, DeviceInformationUpdate update)
    {
        if (update.Id != _budsDeviceId) return;
        SetConnected(false);
    }

    private void SetConnected(bool connected)
    {
        _btConnected = connected;
        if (connected) RefreshUi();
        else _ctx.Post(_ => _tray.Visible = false, null);
    }

    private void OnRefreshTick(object? _) => RefreshUi();

    private void RefreshUi()
    {
        if (!_btConnected) return;

        string leftStr, caseStr, rightStr;
        byte leftPct, casePct, rightPct;

        lock (_dataLock)
        {
            leftStr = _leftStr;
            caseStr = _caseStr;
            rightStr = _rightStr;
            leftPct = _leftPct;
            casePct = _casePct;
            rightPct = _rightPct;
        }

        var icon = RenderTrayIcon(leftPct, casePct, rightPct);

        _ctx.Post(_ =>
        {
            if (!_tray.Visible) _tray.Visible = true;
            var old = _tray.Icon;
            _tray.Icon = icon;
            old?.Dispose();
            _popup.UpdateData(leftStr, caseStr, rightStr, leftPct, casePct, rightPct);
        }, null);
    }

    private static string FormatPct(byte pct, bool charging)
    {
        if (pct > 100) return "--";
        return charging ? $"⚡{pct}%" : $"{pct}%";
    }

    private static Icon RenderTrayIcon(byte leftPct, byte casePct, byte rightPct)
    {
        byte min = 0xFF;
        if (leftPct <= 100) min = Math.Min(min, leftPct);
        if (casePct <= 100) min = Math.Min(min, casePct);
        if (rightPct <= 100) min = Math.Min(min, rightPct);

        var text = min is <= 100 and < 50 ? $"{min}" : "";
        Color color = BatteryPopup.PercentColor(min);

        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.Clear(Color.Transparent);

        if (min <= 100)
        {
            using var arcPen = new Pen(Color.White, 3.5f);
            arcPen.LineJoin = LineJoin.Round;
            g.DrawArc(arcPen, 3, 1, 26, 18, 180, 180);

            using var earBrush = new SolidBrush(Color.White);
            using var earPen = new Pen(Color.White, 1f);
            g.FillEllipse(earBrush, 0, 13, 10, 14);
            g.DrawEllipse(earPen, 0, 13, 10, 14);
            g.FillEllipse(earBrush, 22, 13, 10, 14);
            g.DrawEllipse(earPen, 22, 13, 10, 14);
        }

        if (text.Length > 0)
        {
            var emSize = text.Length > 2 ? 19f : 24f;

            using var textPath = new GraphicsPath();
            using var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            textPath.AddString(text, new FontFamily("Segoe UI"), (int)FontStyle.Bold,
                emSize, new RectangleF(0, 2, size, size), sf);

            using var outline = new Pen(Color.FromArgb(230, 10, 10, 10), 4f);
            outline.LineJoin = LineJoin.Round;
            outline.StartCap = LineCap.Round;
            outline.EndCap = LineCap.Round;
            g.DrawPath(outline, textPath);

            using var fill = new SolidBrush(color);
            g.FillPath(fill, textPath);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public void Dispose()
    {
        if (_btWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _btWatcher.Stop();

        _refreshTimer.Dispose();
        _scanner.Dispose();
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
    }
}