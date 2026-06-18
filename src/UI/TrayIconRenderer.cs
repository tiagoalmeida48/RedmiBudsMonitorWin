using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TrayBatt;

internal static class TrayIconRenderer
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const int IconSize = 32;

    public static Icon Render(byte overallMin, bool budsConnected)
    {
        using var bmp = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bmp);

        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.Clear(Color.Transparent);

        DrawHeadphone(g, budsConnected ? Color.White : Color.FromArgb(140, 140, 140));

        if (overallMin.IsValid() && overallMin < 50) DrawBatteryLabel(g, overallMin);

        return BitmapToIcon(bmp);
    }

    private static void DrawHeadphone(Graphics g, Color color)
    {
        using var arc = new Pen(color, 3.5f) { LineJoin = LineJoin.Round };
        g.DrawArc(arc, 3, 1, 26, 18, 180, 180);

        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 1f);
        g.FillEllipse(brush, 0, 13, 10, 14);
        g.DrawEllipse(pen, 0, 13, 10, 14);
        g.FillEllipse(brush, 22, 13, 10, 14);
        g.DrawEllipse(pen, 22, 13, 10, 14);
    }

    private static void DrawBatteryLabel(Graphics g, byte percent)
    {
        var text = $"{percent}";
        var emSize = text.Length > 2 ? 19f : 24f;

        using var fontFamily = new FontFamily("Segoe UI");
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var path = new GraphicsPath();
        path.AddString(text, fontFamily, (int)FontStyle.Bold, emSize, new RectangleF(0, 2, IconSize, IconSize), sf);

        using var outline = new Pen(Color.FromArgb(230, 10, 10, 10), 4f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawPath(outline, path);

        using var fill = new SolidBrush(percent.ToColor());
        g.FillPath(fill, path);
    }

    private static Icon BitmapToIcon(Bitmap bmp)
    {
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
}