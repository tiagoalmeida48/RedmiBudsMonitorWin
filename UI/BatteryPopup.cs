using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace RedmiBudsMonitor;

internal sealed class BatteryPopup : Form
{
    private BatterySnapshot Snapshot
    {
        get;
        set
        {
            field = value;
            if (Visible) Invalidate();
        }
    } = BatterySnapshot.Empty;

    public BatteryPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(28, 28, 28);
        Width = 220;
        Height = 155;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(0);
    }

    public void UpdateData(BatterySnapshot snapshot) => Snapshot = snapshot;

    public void ToggleNearTray()
    {
        if (Visible)
        {
            Hide();
            return;
        }

        var area = Screen.PrimaryScreen!.WorkingArea;
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
        DrawRow(g, 0, BatteryDevice.Left, "Esquerdo", Snapshot.Left.Label, Snapshot.Left.Pct);
        DrawRow(g, 1, BatteryDevice.Case, "Caixa", Snapshot.Case.Label, Snapshot.Case.Pct);
        DrawRow(g, 2, BatteryDevice.Right, "Direito", Snapshot.Right.Label, Snapshot.Right.Pct);
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

    private void DrawRow(Graphics g, int index, BatteryDevice device, string name, string label, byte pct)
    {
        var y = 46f + index * 34f;
        DrawRowIcon(g, device, y);
        DrawRowName(g, name, y);
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

    private static void DrawRowName(Graphics g, string name, float y)
    {
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(200, 200, 205));
        var size = g.MeasureString(name, font);
        g.DrawString(name, font, brush, 50f, y + (26f - size.Height) / 2f);
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
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235));

        var stemX = isRight ? 0f : -3.5f;
        g.FillPath(brush, RoundRect(stemX, -7f, 3.5f, 13f, 1.5f));

        var headX = isRight ? -4f : 0f;
        g.FillEllipse(brush, headX, -7f, 6.5f, 7.5f);

        using var dark = new SolidBrush(Color.FromArgb(28, 28, 30));
        g.FillEllipse(dark, stemX + 1f, -4f, 1.5f, 1.5f);
    }

    private static void DrawCase(Graphics g)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 235, 235));
        g.FillPath(brush, RoundRect(-6.5f, -2f, 13f, 8.5f, 3f));

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