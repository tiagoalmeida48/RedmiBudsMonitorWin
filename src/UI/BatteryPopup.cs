using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TrayBatt;

internal sealed class BatteryPopup : Form
{
    private const int   BaseHeight       = 155;
    private const float DeviceSectionTop = 150f;
    private const float DeviceRowsTop    = 180f;
    private const float DeviceRowHeight  = 28f;

    private BatterySnapshot Snapshot { get; set; } = BatterySnapshot.Empty;

    private IReadOnlyList<DeviceBattery> _devices = [];

    public BatteryPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(28, 28, 30);
        Width = 250;
        Height = BaseHeight;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(0);
    }

    public void UpdateData(BatterySnapshot snapshot, IReadOnlyList<DeviceBattery> devices)
    {
        Snapshot = snapshot;
        _devices = devices;
        ApplyLayout();
        if (Visible) Invalidate();
    }

    private void ApplyLayout()
    {
        var height = _devices.Count == 0
            ? BaseHeight
            : (int)(DeviceRowsTop + _devices.Count * DeviceRowHeight + 10f);
        if (height == Height) return;

        var bottom = Top + Height;
        Height = height;
        Top = bottom - height;
    }

    public void ToggleNearTray()
    {
        if (Visible)
        {
            Hide();
            return;
        }

        var area = Screen.FromPoint(MousePosition).WorkingArea;
        Location = new Point(area.Right - Width - 14, area.Bottom - Height - 14);
        Show();
        Activate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.SmoothingMode = SmoothingMode.HighQuality;

        DrawBackground(g);
        DrawTitle(g);
        DrawSeparator(g);
        DrawRow(g, 0, BatteryDevice.Left, "Esquerdo", Snapshot.Left.Label, Snapshot.Left.Pct, Snapshot.Left.InCase);
        DrawRow(g, 1, BatteryDevice.Case, "Caixa", Snapshot.Case.Label, Snapshot.Case.Pct, inCase: null);
        DrawRow(g, 2, BatteryDevice.Right, "Direito", Snapshot.Right.Label, Snapshot.Right.Pct, Snapshot.Right.InCase);
        DrawDevices(g);
    }

    private void DrawDevices(Graphics g)
    {
        var devices = _devices;
        if (devices.Count == 0) return;

        using var separator = new Pen(Color.FromArgb(45, 45, 50), 1);
        g.DrawLine(separator, 16, DeviceSectionTop, Width - 16, DeviceSectionTop);

        using var headerFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var headerBrush = new SolidBrush(Color.FromArgb(140, 140, 148));
        g.DrawString("Outros dispositivos", headerFont, headerBrush, 16f, DeviceSectionTop + 8f);

        using var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        using var nameBrush = new SolidBrush(Color.FromArgb(200, 200, 205));
        using var valueFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var nameFormat = new StringFormat(StringFormatFlags.NoWrap)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            LineAlignment = StringAlignment.Center,
        };

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var y = DeviceRowsTop + i * DeviceRowHeight;
            var label = device.Pct.ToLabel(false);

            using var valueBrush = new SolidBrush(device.Pct.ToColor());
            var valueSize = g.MeasureString(label, valueFont);
            var valueX = Width - 16f - valueSize.Width;
            g.DrawString(label, valueFont, valueBrush, valueX, y + (DeviceRowHeight - valueSize.Height) / 2f);

            var nameRect = new RectangleF(16f, y, valueX - 24f, DeviceRowHeight);
            g.DrawString(device.Name, nameFont, nameBrush, nameRect, nameFormat);
        }
    }

    private void DrawBackground(Graphics g)
    {
        using var bg = new SolidBrush(Color.FromArgb(28, 28, 30));
        using var border = new Pen(Color.FromArgb(60, 60, 65), 1);
        using var path = RoundRect(0, 0, Width - 1, Height - 1, 14);
        g.FillPath(bg, path);
        g.DrawPath(border, path);
    }

    private static void DrawTitle(Graphics g)
    {
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 245));
        g.DrawString("Redmi Buds 5", font, brush, 16f, 12f);
    }

    private void DrawSeparator(Graphics g)
    {
        using var pen = new Pen(Color.FromArgb(45, 45, 50), 1);
        g.DrawLine(pen, 16, 38, Width - 16, 38);
    }

    private void DrawRow(Graphics g, int index, BatteryDevice device, string name, string label, byte pct, bool? inCase)
    {
        var y = 46f + index * 34f;
        DrawRowIcon(g, device, y);
        var nameWidth = DrawRowName(g, name, y);
        if (inCase is { } state) DrawPresenceTag(g, 50f + nameWidth + 8f, y, state);
        DrawRowValue(g, label, pct, y);
    }

    private void DrawRowIcon(Graphics g, BatteryDevice device, float y)
    {
        using var bg = new SolidBrush(Color.FromArgb(44, 44, 46));
        g.FillEllipse(bg, 16f, y, 26f, 26f);

        var state = g.Save();
        g.TranslateTransform(29f, y + 13f);
        if (device == BatteryDevice.Case) DrawCase(g);
        else DrawEarbud(g, device);
        g.Restore(state);
    }

    private static float DrawRowName(Graphics g, string name, float y)
    {
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(200, 200, 205));
        var size = g.MeasureString(name, font);
        g.DrawString(name, font, brush, 50f, y + (26f - size.Height) / 2f);
        return size.Width;
    }

    private static void DrawPresenceTag(Graphics g, float x, float y, bool inCase)
    {
        var (fill, text) = inCase
            ? (Color.FromArgb(70, 70, 76), "na caixa")
            : (Color.FromArgb(40, 110, 70), "em uso");

        using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var width = size.Width + 10f;
        const float height = 15f;
        var tagY = y + (26f - height) / 2f;

        using var bg = new SolidBrush(fill);
        using var path = RoundRect(x, tagY, width, height, height);
        g.FillPath(bg, path);

        using var textBrush = new SolidBrush(Color.FromArgb(225, 225, 230));
        g.DrawString(text, font, textBrush, x + 5f, tagY + (height - size.Height) / 2f);
    }

    private void DrawRowValue(Graphics g, string label, byte pct, float y)
    {
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var brush = new SolidBrush(pct.ToColor());
        var size = g.MeasureString(label, font);
        g.DrawString(label, font, brush, Width - 16f - size.Width, y + (26f - size.Height) / 2f);
    }

    private static void DrawEarbud(Graphics g, BatteryDevice side)
    {
        var isRight = side == BatteryDevice.Right;
        var stemX = isRight ? 0f : -3.5f;
        var headX = isRight ? -4f : 0f;

        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235));
        using var stem = RoundRect(stemX, -7f, 3.5f, 13f, 1.5f);
        g.FillPath(brush, stem);
        g.FillEllipse(brush, headX, -7f, 6.5f, 7.5f);

        using var dark = new SolidBrush(Color.FromArgb(28, 28, 30));
        g.FillEllipse(dark, stemX + 1f, -4f, 1.5f, 1.5f);
    }

    private static void DrawCase(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235));
        using var body = RoundRect(-6.5f, -2f, 13f, 8.5f, 3f);
        g.FillPath(brush, body);

        using var lid = new Pen(brush, 1.5f);
        g.DrawArc(lid, -6.5f, -6f, 13f, 11f, 180, 180);

        using var dark = new SolidBrush(Color.FromArgb(28, 28, 30));
        g.FillRectangle(dark, -2f, 3f, 4f, 1f);
    }

    private static GraphicsPath RoundRect(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return path;
    }
}