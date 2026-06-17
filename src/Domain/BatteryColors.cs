namespace RedmiBudsMonitor;

internal static class BatteryColors
{
    private const byte MaxValid = 100;

    internal static bool IsValid(this byte pct) => pct <= MaxValid;

    internal static Color ToColor(this byte pct) => pct switch
    {
        > MaxValid => Color.FromArgb(110, 110, 110),
        >= 50 => Color.FromArgb(72, 199, 116),
        >= 20 => Color.FromArgb(255, 159, 10),
        _ => Color.FromArgb(255, 69, 58),
    };

    internal static string ToLabel(this byte pct, bool charging)
    {
        if (pct > MaxValid) return "--";
        return charging ? $"⚡{pct}%" : $"{pct}%";
    }
}