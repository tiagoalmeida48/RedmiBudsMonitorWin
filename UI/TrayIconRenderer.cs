using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RedmiBudsMonitor;

internal static class TrayIconRenderer
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const int IconSize = 32;

    public static Icon Render(BatterySnapshot snapshot)
    {
        var min = snapshot.MinPercent;

        using var bmp = new Bitmap(IconSize, IconSize);
        using var g = Graphics.FromImage(bmp);

        ConfigureGraphics(g);

        if (min.IsValid) DrawHeadphone(g);
        if (min.IsValid && min < 50) DrawBatteryLabel(g, min);

        return BitmapToIcon(bmp);
    }

    private static void ConfigureGraphics(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.Clear(Color.Transparent);
    }

    private static void DrawHeadphone(Graphics g)
    {
        using var arc = new Pen(Color.White, 3.5f);
        arc.LineJoin = LineJoin.Round;
        g.DrawArc(arc, 3, 1, 26, 18, 180, 180);

        using var brush = new SolidBrush(Color.White);
        using var pen = new Pen(Color.White, 1f);
        g.FillEllipse(brush, 0, 13, 10, 14);
        g.DrawEllipse(pen, 0, 13, 10, 14);
        g.FillEllipse(brush, 22, 13, 10, 14);
        g.DrawEllipse(pen, 22, 13, 10, 14);
    }

    private static void DrawBatteryLabel(Graphics g, byte percent)
    {
        var text = $"{percent}";
        var emSize = text.Length > 2 ? 19f : 24f;

        using var path = new GraphicsPath();
        using var sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;
        path.AddString(text, new FontFamily("Segoe UI"), (int)FontStyle.Bold,
            emSize, new RectangleF(0, 2, IconSize, IconSize), sf);

        using var outline = new Pen(Color.FromArgb(230, 10, 10, 10), 4f);
        outline.LineJoin = LineJoin.Round;
        outline.StartCap = LineCap.Round;
        outline.EndCap = LineCap.Round;
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