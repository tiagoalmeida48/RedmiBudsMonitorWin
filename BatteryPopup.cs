using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace RedmiBudsMonitor;

internal sealed class BatteryPopup : Form
{
    private string _leftStr = "--";
    private string _caseStr = "--";
    private string _rightStr = "--";
    private byte _leftPct = 0xFF;
    private byte _casePct = 0xFF;
    private byte _rightPct = 0xFF;

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

    public void UpdateData(string leftStr, string caseStr, string rightStr, byte leftPct, byte casePct, byte rightPct)
    {
        _leftStr = leftStr;
        _caseStr = caseStr;
        _rightStr = rightStr;
        _leftPct = leftPct;
        _casePct = casePct;
        _rightPct = rightPct;
        if (Visible) Invalidate();
    }

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

        using var bgBrush = new SolidBrush(Color.FromArgb(28, 28, 30));
        using var borderPen = new Pen(Color.FromArgb(60, 60, 65), 1);
        using var path = RoundRect(0, 0, Width - 1, Height - 1, 14);

        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 235, 245));
        g.DrawString("Redmi Buds 5", titleFont, titleBrush, 16f, 12f);

        using var sepPen = new Pen(Color.FromArgb(45, 45, 50), 1);
        g.DrawLine(sepPen, 16, 38, Width - 16, 38);

        DrawRow(g, 0, true, "Esquerdo", _leftStr, _leftPct, isRight: false);
        DrawRow(g, 1, false, "Caixa", _caseStr, _casePct, isRight: false);
        DrawRow(g, 2, true, "Direito", _rightStr, _rightPct, isRight: true);
    }

    private void DrawRow(Graphics g, int index, bool isEarbud, string name, string value, byte pct, bool isRight)
    {
        var y = 46f + index * 34f;

        using var iconBg = new SolidBrush(Color.FromArgb(44, 44, 46));
        g.FillEllipse(iconBg, 16f, y, 26f, 26f);

        var st = g.Save();
        g.TranslateTransform(29f, y + 13f);
        if (isEarbud) DrawEarbud(g, isRight);
        else DrawCase(g);
        g.Restore(st);

        using var nameFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        using var nameBrush = new SolidBrush(Color.FromArgb(200, 200, 205));
        var nsz = g.MeasureString(name, nameFont);
        g.DrawString(name, nameFont, nameBrush, 50f, y + (26f - nsz.Height) / 2f);

        using var valueFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var valueBrush = new SolidBrush(PercentColor(pct));
        var vsz = g.MeasureString(value, valueFont);
        g.DrawString(value, valueFont, valueBrush, Width - 16f - vsz.Width, y + (26f - vsz.Height) / 2f);
    }

    private static void DrawEarbud(Graphics g, bool isRight)
    {
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

    internal static Color PercentColor(byte pct) => pct switch
    {
        > 100 => Color.FromArgb(110, 110, 110),
        >= 50 => Color.FromArgb(72, 199, 116),
        >= 20 => Color.FromArgb(255, 159, 10),
        _ => Color.FromArgb(255, 69, 58),
    };
}