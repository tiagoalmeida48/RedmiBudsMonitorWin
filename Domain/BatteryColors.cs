namespace RedmiBudsMonitor;

internal static class BatteryColors
{
    private const byte MaxValid = 100;

    extension(byte pct)
    {
        internal bool IsValid => pct <= MaxValid;

        internal Color ToColor() => pct switch
        {
            > MaxValid => Color.FromArgb(110, 110, 110),
            >= 50 => Color.FromArgb(72, 199, 116),
            >= 20 => Color.FromArgb(255, 159, 10),
            _ => Color.FromArgb(255, 69, 58),
        };

        internal string ToLabel(bool charging)
        {
            if (pct > MaxValid) return "--";
            return charging ? $"⚡{pct}%" : $"{pct}%";
        }
    }
}