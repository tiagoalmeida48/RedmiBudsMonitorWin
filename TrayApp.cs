using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace RedmiBudsMonitor;

/// <summary>
/// Gerencia o ícone da bandeja e o popup de bateria.
///
/// Visibilidade do ícone controlada pelo estado de conexão BT (DeviceWatcher).
/// Dados de bateria atualizados a cada 10 segundos via BLE advertisement.
/// </summary>
internal sealed class TrayApp : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly BleScanner             _scanner;
    private readonly ContextMenuStrip       _menu;
    private readonly NotifyIcon             _tray;
    private readonly BatteryPopup           _popup;
    private readonly SynchronizationContext _ctx;
    private readonly System.Threading.Timer _refreshTimer;
    private readonly DeviceWatcher          _btWatcher;

    // ── Último estado BLE recebido (protegido por _dataLock) ─────────────────
    private readonly object _dataLock = new();
    private byte   _lastCase = 0xFF;
    private string _leftStr  = "--";
    private string _caseStr  = "--";
    private string _rightStr = "--";
    private byte   _leftPct  = 0xFF;
    private byte   _casePct  = 0xFF;
    private byte   _rightPct = 0xFF;

    // ── Estado de conexão BT ─────────────────────────────────────────────────
    private volatile bool    _btConnected  = false;
    private volatile string? _budsDeviceId = null;

    private const string BudsNameFilter = "Redmi Buds";

    public TrayApp()
    {
        _ctx = SynchronizationContext.Current
               ?? new WindowsFormsSynchronizationContext();

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Sair", null, (_, _) => Application.Exit());

        _popup = new BatteryPopup();

        _tray = new NotifyIcon
        {
            Icon             = RenderTrayIcon(0xFF, 0xFF, 0xFF),
            Visible          = false,   // oculto até confirmar conexão BT
            Text             = "Redmi Buds 5",
            ContextMenuStrip = _menu,
        };

        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _popup.ToggleNearTray();
        };

        // Atualiza conteúdo do ícone a cada 10 s
        _refreshTimer = new System.Threading.Timer(OnRefreshTick, null, 10_000, 10_000);

        // Monitora conexão/desconexão do dispositivo BT pareado
        string aqsFilter = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        _btWatcher = DeviceInformation.CreateWatcher(
            aqsFilter,
            new[] { "System.Devices.Aep.IsConnected" });
        _btWatcher.Added   += OnBtDeviceAdded;
        _btWatcher.Updated += OnBtDeviceUpdated;
        _btWatcher.Removed += OnBtDeviceRemoved;
        _btWatcher.Start();

        _scanner = new BleScanner();
        _scanner.OnBudsData += OnData;
    }

    public void Start() => _scanner.Start();

    // ── Recepção de dados BLE ─────────────────────────────────────────────────

    private void OnData(BudsAdvertisement buds)
    {
        byte caseDisplay;
        lock (_dataLock)
        {
            if (buds.HasCase) _lastCase = buds.BatteryCase;
            caseDisplay = _lastCase;
        }

        byte leftPct  = buds.HasLeft  ? buds.BatteryLeft  : (byte)0xFF;
        byte rightPct = buds.HasRight ? buds.BatteryRight : (byte)0xFF;
        byte casePct  = caseDisplay <= 100 ? caseDisplay  : (byte)0xFF;

        bool leftCharging  = buds.IsLeftInCase  && leftPct  < 100 && casePct > 0;
        bool rightCharging = buds.IsRightInCase && rightPct < 100 && casePct > 0;
        bool caseCharging  = buds.IsCaseCharging;

        lock (_dataLock)
        {
            _leftStr  = FormatField(leftPct,  leftCharging);
            _caseStr  = FormatField(casePct,  caseCharging);
            _rightStr = FormatField(rightPct, rightCharging);
            _leftPct  = leftPct;
            _casePct  = casePct;
            _rightPct = rightPct;
        }
    }

    // ── Monitoramento de conexão BT (DeviceWatcher) ───────────────────────────

    private void OnBtDeviceAdded(DeviceWatcher sender, DeviceInformation info)
    {
        if (!info.Name.Contains(BudsNameFilter, StringComparison.OrdinalIgnoreCase)) return;

        _budsDeviceId = info.Id;
        bool connected = info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var v)
                         && v is bool b && b;
        ApplyConnectionState(connected);
    }

    private void OnBtDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (update.Id != _budsDeviceId) return;
        if (!update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var v)) return;

        bool connected = v is bool b && b;
        ApplyConnectionState(connected);
    }

    private void OnBtDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (update.Id != _budsDeviceId) return;
        ApplyConnectionState(false);
    }

    private void ApplyConnectionState(bool connected)
    {
        _btConnected = connected;
        if (connected)
            RefreshUi(); // mostra e atualiza imediatamente ao conectar
        else
            _ctx.Post(_ => _tray.Visible = false, null);
    }

    // ── Timer — atualiza conteúdo a cada 10 s ─────────────────────────────────

    private void OnRefreshTick(object? _) => RefreshUi();

    private void RefreshUi()
    {
        if (!_btConnected) return;

        string leftStr, caseStr, rightStr;
        byte   leftPct, casePct, rightPct;

        lock (_dataLock)
        {
            leftStr  = _leftStr;  caseStr  = _caseStr;  rightStr  = _rightStr;
            leftPct  = _leftPct;  casePct  = _casePct;  rightPct  = _rightPct;
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

    // ── Formatação ────────────────────────────────────────────────────────────

    private static string FormatField(byte pct, bool charging)
    {
        if (pct > 100) return "--";
        return charging ? $"⚡{pct}%" : $"{pct}%";
    }

    // ── Ícone da bandeja ──────────────────────────────────────────────────────

    private static Icon RenderTrayIcon(byte leftPct, byte casePct, byte rightPct)
    {
        byte minPct = 0xFF;
        if (leftPct  <= 100) minPct = Math.Min(minPct, leftPct);
        if (casePct  <= 100) minPct = Math.Min(minPct, casePct);
        if (rightPct <= 100) minPct = Math.Min(minPct, rightPct);

        string text  = (minPct <= 100 && minPct < 50) ? $"{minPct}" : "";
        Color  color = BatteryPopup.PercentColor(minPct);

        const int W = 32, H = 32;
        using var bmp = new Bitmap(W, H);
        using var g   = Graphics.FromImage(bmp);

        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.Clear(Color.Transparent);

        // ── 1. Headphone branco ───────────────────────────────────────────────
        if (minPct <= 100)
        {
            using var arcPen = new Pen(Color.White, 3.5f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            g.DrawArc(arcPen, 3, 1, 26, 18, 180, 180);

            using var earBrush = new SolidBrush(Color.White);
            using var earPen   = new Pen(Color.White, 1f);
            g.FillEllipse(earBrush, 0, 13, 10, 14);
            g.DrawEllipse(earPen,   0, 13, 10, 14);
            g.FillEllipse(earBrush, 22, 13, 10, 14);
            g.DrawEllipse(earPen,   22, 13, 10, 14);
        }

        // ── 2. Número (apenas se bateria < 50%) ───────────────────────────────
        if (text.Length > 0)
        {
            float emSize = text.Length > 2 ? 19f : 24f;

            using var textPath = new System.Drawing.Drawing2D.GraphicsPath();
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            textPath.AddString(text, new FontFamily("Segoe UI"), (int)FontStyle.Bold,
                               emSize, new RectangleF(0, 2, W, H), sf);

            using var outlinePen = new Pen(Color.FromArgb(230, 10, 10, 10), 4f)
            {
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap   = System.Drawing.Drawing2D.LineCap.Round,
            };
            g.DrawPath(outlinePen, textPath);

            using var fillBrush = new SolidBrush(color);
            g.FillPath(fillBrush, textPath);
        }

        IntPtr hIcon = bmp.GetHicon();
        try   { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        var status = _btWatcher.Status;
        if (status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _btWatcher.Stop();

        _refreshTimer.Dispose();
        _scanner.Dispose();
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
    }
}
